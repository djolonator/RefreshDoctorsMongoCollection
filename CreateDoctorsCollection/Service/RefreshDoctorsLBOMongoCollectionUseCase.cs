using System.Net;
using System.Xml;
using System.IO.Compression;
using System.Runtime.Caching;
using Vuz.Models;
using CreateDoctorsCollection.Models;
using CreateDoctorsCollection.Enums;
using CreateDoctorsCollection.Repository;
using CreateDoctorsCollection.Utls;


namespace CreateDoctorsCollection.Service
{
    public class RefreshDoctorsLBOMongoCollectionUseCase : IRefreshDoctorsLBOMongoCollectionUseCase
    {
        private readonly IDoctorsRepository _doctorsRepository;
        const CacheKey _key = CacheKey.DoctorsLBO;
        private readonly HttpClient _client;
        private string _collectionName = "Doctors";
        private string _xmlPrimarDoctorTagName = "sif_lekar";
        private string _xmlPrimarDoctorID = "oznaka";
        private string _xmlPrimarNameFiled = "ime";
        private string _xmlPrimarSurnameFiled = "prezime";
        private string _xmlPrimarLBONumberFiled = "lbo";
        private string _xmlSekundarDoctorTagName = "LekarSekundar";
        private string _xmlSekundarDoctorID = "SifraLekara";
        private string _xmlSekundarNameFiled = "ImeLekara";
        private string _xmlSekundarSurnameFiled = "PrezimeLekara";
        private string _xmlSekundarLBONumberFiled = "LBO";
        private string _portalRfzoUsername = "";
        private string _portalRfzoPassword = "";
        private string _portalRfzoBaseAdress = "http://portal.rfzo.rs/";
        private string _lastPartOfUriPrimar = "cPrimar.htm";
        private string _primarFilenameContains = "Sifarnik_cPrimar";
        private string _sekundarFilenameContains = "Lekar_Sekundar";
        private int _refreshIntervalInMinutes = 1;
        private string _sekundarUri = "http://portal.rfzo.rs/zus/EFsekundarna/XMLPrikazSifarnikazaSZZ.zip";

        public RefreshDoctorsLBOMongoCollectionUseCase(IDoctorsRepository doctorsRepository)
        {
            _doctorsRepository = doctorsRepository;

            _client = new HttpClient(new HttpClientHandler()
            {
                Credentials = new NetworkCredential(_portalRfzoUsername, _portalRfzoPassword)
            });
        }

        public async Task<string> Execute()
        {
            string message = string.Empty;

            if (CheckIfCanRefreshDoctorsLBOCollection())
            {
                //stavljeno da se enpoint ne moze pozivati uzastopno, mora proci 1 minuta izmedju dva poziva
                StoreRequestTime();
                var doctorsPrimar = await GetPrimarDoctorsFromXMLFile();
                var doctorsSekundar = await GetSekundarDoctorsFromXMLFile();
                var allDoctorsMerged = MergeLists(doctorsPrimar, doctorsSekundar);
                var validationResult = ValidateDoctorsEntries(allDoctorsMerged);

                if (validationResult.IsSucess)
                {
                    await _doctorsRepository.DropDoctorsCollectionAsync(_collectionName);
                    await _doctorsRepository.CreateDoctorsCollectionAsync(_collectionName);
                    await _doctorsRepository.AddDoctorsToCollectionAsync(validationResult.TrimedDoctors);

                    message = "Uspešno refresovana Doctors kolekcija.";
                }
                else
                    message = validationResult.Message + " Doctors kolekcija nije refresovana.";
            }
            else
                message = $"Između dva poziva mora da prođe {_refreshIntervalInMinutes} minuta.";

            return message;
        }

        private List<StorageDoctorsModel> MergeLists(List<StorageDoctorsModel> list1, List<StorageDoctorsModel> list2)
        {
            var dict = list1.ToDictionary(p => p.LBO, p => p);

            foreach (var item in list2)
            {
                if (!dict.ContainsKey(item.LBO))
                {
                    dict[item.LBO] = item;
                }
                else if (!string.IsNullOrEmpty(item.BrojPecata))
                {
                    dict[item.LBO] = item;
                }
            }
            return dict.Values.ToList();
        }

        private async Task<List<StorageDoctorsModel>> GetSekundarDoctorsFromXMLFile()
        {
            var xmlString = await DownloadXMLDoctors(new Uri(_sekundarUri), _sekundarFilenameContains);
            return await ReadXMLDoctorsSekundar(xmlString, _xmlSekundarDoctorTagName, _xmlSekundarNameFiled, _xmlSekundarSurnameFiled, _xmlSekundarLBONumberFiled, _xmlSekundarDoctorID);
        }

        private async Task<List<StorageDoctorsModel>> GetPrimarDoctorsFromXMLFile()
        {
            var fileNameForPrimar = await GetFileNameForSifarnik();
            var uri = new Uri(_portalRfzoBaseAdress + _portalRfzoUsername + "/" + fileNameForPrimar);
            var xmlString = await DownloadXMLDoctors(uri, _primarFilenameContains);
            return ReadXMLDoctorsPrimar(xmlString, _xmlPrimarDoctorTagName, _xmlPrimarNameFiled, _xmlPrimarSurnameFiled, _xmlPrimarLBONumberFiled, _xmlPrimarDoctorID);
        }

        private async Task<string> GetFileNameForSifarnik()
        {
            string filename = string.Empty;

            var uri = new Uri(_portalRfzoBaseAdress + _portalRfzoUsername + "/" + _lastPartOfUriPrimar);
            var html = await _client.GetStringAsync(uri);

            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            filename = doc.DocumentNode.Descendants("a")
                .FirstOrDefault(n => n.GetAttributeValue("href", "").Contains(_primarFilenameContains))
                ?.GetAttributeValue("href", "");

            return filename;
        }

        private async Task<string> DownloadXMLDoctors(Uri uri, string fileName)
        {
            string xmlContent = string.Empty;
            var response = new HttpResponseMessage();

            response = await _client.GetAsync(uri);
            if (response.IsSuccessStatusCode)
            {
                byte[] fileContent = await response.Content.ReadAsByteArrayAsync();

                using (var memoryStream = new MemoryStream(fileContent))
                {
                    using (var archive = new ZipArchive(memoryStream))
                    {
                        using (var entryStream = archive.Entries.Where(e => e.Name.StartsWith(fileName)).FirstOrDefault().Open())
                        using (var streamReader = new StreamReader(entryStream))
                        {
                            xmlContent = await streamReader.ReadToEndAsync();
                        }
                    }
                }
            }
            return xmlContent;
        }

        private List<StorageDoctorsModel> ReadXMLDoctorsPrimar(string xmlString, string tagName, string name, string surname, string lbo, string doctorId)
        {
            var doctors = new List<StorageDoctorsModel>();

            using (StringReader stringReader = new StringReader(xmlString))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xmlString);
                
                XmlNodeList sifLekarNodes = doc.GetElementsByTagName(tagName);
                foreach (XmlNode node in sifLekarNodes)
                {
                    var doctor = new StorageDoctorsModel();
                    doctor.BrojPecata = node.SelectSingleNode(doctorId)?.InnerText;
                    doctor.Ime = node.SelectSingleNode(name)?.InnerText;
                    doctor.Prezime = node.SelectSingleNode(surname)?.InnerText;
                    doctor.LBO = node.SelectSingleNode(lbo)?.InnerText;
                    
                    doctors.Add(doctor);
                }
                
            }
            return doctors;
        }

        private async Task<List<StorageDoctorsModel>> ReadXMLDoctorsSekundar(string xmlString, string tagName, string name, string surname, string lbo, string doctorId)
        {
            var doctors = new List<StorageDoctorsModel>();

            using (StringReader stringReader = new StringReader(xmlString))
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ConformanceLevel = ConformanceLevel.Document;
                settings.Async = true;
                settings.IgnoreComments = true;

                using (XmlReader reader = XmlReader.Create(stringReader, settings))
                {
                    while (await reader.ReadAsync())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == (tagName))
                        {
                            var doctor = new StorageDoctorsModel();
                            
                            if (!reader.ReadToDescendant(name))
                                continue;
                            else
                                doctor.Ime = await reader.ReadElementContentAsStringAsync();
                            if (!reader.ReadToNextSibling(surname))
                                continue;
                            else
                                doctor.Prezime = await reader.ReadElementContentAsStringAsync();
                            if (!reader.ReadToNextSibling(lbo))
                                continue;
                            else
                                doctor.LBO = await reader.ReadElementContentAsStringAsync();

                            doctors.Add(doctor);
                        }
                    }
                }
            }
            return doctors;
        }

        private RefreshDoctorLBOModel ValidateDoctorsEntries(List<StorageDoctorsModel> doctors)
        {
            var validationResult = new RefreshDoctorLBOModel();
            int minimumEntriesCount = 30000;
            int maximumInvalidEntriesCount = 1000;
            var nullEntriesValues = FindNullEntriesValues(doctors);
            var invalidEntriesValues = FindInvalidEntriesValues(doctors);

            if (doctors.Count <= minimumEntriesCount)
                validationResult.Message = $"Prazna ili nepotpuna lista (manje od {minimumEntriesCount} unosa) doktora iz xml fajlova.";
            else if (nullEntriesValues.Count() > maximumInvalidEntriesCount)
                validationResult.Message = $"Prazne ili null vrednosti za ime, prezime ili lbo iz xml fajla za vise od {maximumInvalidEntriesCount} unosa.";
            else if (invalidEntriesValues.Count() > maximumInvalidEntriesCount)
                validationResult.Message = $"Nevalidne vrednosti za ime, prezime ili lbo iz xml fajla za vise od {maximumInvalidEntriesCount} unosa.";
            else
            {
                validationResult.IsSucess = true;
                validationResult.TrimedDoctors = doctors.Except(nullEntriesValues).Except(invalidEntriesValues).ToList();
            }

            return validationResult;
        }

        private List<StorageDoctorsModel> FindNullEntriesValues(List<StorageDoctorsModel> doctors)
        {
            var doctorsWithNullValues = doctors.Where(d => string.IsNullOrEmpty(d.LBO) || string.IsNullOrEmpty(d.Ime) || string.IsNullOrEmpty(d.Prezime)).ToList();
            return doctorsWithNullValues;
        }

        private List<StorageDoctorsModel> FindInvalidEntriesValues(List<StorageDoctorsModel> doctors)
        {
            var invalidEntries = new List<StorageDoctorsModel>();
            doctors.ForEach(doctor =>
            {
                var ime = RegexValidations.ValidateNoDigitsString(doctor.Ime);
                var prezime = RegexValidations.ValidateNoDigitsString(doctor.Prezime);
                var lbo = RegexValidations.ValidateLBO(doctor.LBO);

                if (!ime || !prezime || !lbo)
                    invalidEntries.Add(doctor);
            });

            return invalidEntries;
        }

        private void StoreRequestTime()
        {
            MemoryCache.Default.Add(_key.ToString(), "Added", new CacheItemPolicy
            { AbsoluteExpiration = new DateTimeOffset(DateTime.Now.AddMinutes(_refreshIntervalInMinutes)) });
        }

        private bool CheckIfCanRefreshDoctorsLBOCollection()
        {
            return !MemoryCache.Default.Contains(_key.ToString()) ? true : false;
        }
    }
}

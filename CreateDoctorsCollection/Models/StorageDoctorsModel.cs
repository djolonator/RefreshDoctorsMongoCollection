using MongoDB.Bson.Serialization.Attributes;

namespace CreateDoctorsCollection.Models
{
    [BsonIgnoreExtraElements]
    public class StorageDoctorsModel
    {
        public string Ime { get; set; }
        public string Prezime { get; set; }
        public string BrojPecata { get; set; }
        public string LBO { get; set; }
    }
}

using CreateDoctorsCollection.Models;


namespace Vuz.Models
{
    public class RefreshDoctorLBOModel
    {
        public List<StorageDoctorsModel> TrimedDoctors { get; set; }
        public string Message { get; set; }
        public bool IsSucess { get; set; }
    }
}

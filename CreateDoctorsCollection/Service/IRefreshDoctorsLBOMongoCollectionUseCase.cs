using System.Threading.Tasks;

namespace CreateDoctorsCollection.Service
{
    public interface IRefreshDoctorsLBOMongoCollectionUseCase
    {
        Task<string> Execute();
    }
}

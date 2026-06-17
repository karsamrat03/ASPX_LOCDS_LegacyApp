using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LOCDS.DAL.Abstractions
{
    public interface IRepository<T>
    {
        Task<T?> GetById(long id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<T>> GetAll(CancellationToken cancellationToken = default);
        Task<long> Add(T entity, CancellationToken cancellationToken = default);
        Task<bool> Update(T entity, CancellationToken cancellationToken = default);
        Task<bool> Delete(long id, CancellationToken cancellationToken = default);
    }
}

using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DrugInventoryPro // หรือ namespace หลักของโปรเจกต์คุณ
{
    public static class EFExtensions
    {
        public static async Task<HashSet<T>> ToHashSetAsync<T>(
            this IQueryable<T> source,
            CancellationToken cancellationToken = default)
        {
            var list = await source.ToListAsync(cancellationToken);
            return list.ToHashSet();
        }
    }
}
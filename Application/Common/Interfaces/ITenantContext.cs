using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface ITenantContext
    {
        Guid OrgId { get; }
        void SetTenantId(Guid orgId);
    }
}

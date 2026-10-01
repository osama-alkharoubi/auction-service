using Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class TenantContext : ITenantContext
    {
        public Guid OrgId { get; private set; }

        public void SetTenantId(Guid orgId)
        {
            OrgId = orgId;
        }
    }
}

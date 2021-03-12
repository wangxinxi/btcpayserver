using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BTCPayServer.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Fido2
{
    public class Fido2Service
    {
        private readonly ApplicationDbContextFactory _contextFactory;

        public Fido2Service(ApplicationDbContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<Fido2Credential>> GetDevices(string userId)
        {
            await using var context = _contextFactory.CreateContext();
            return await context.Fido2Credentials
                .Where(device => device.ApplicationUserId == userId)
                .ToListAsync();
        }

        public async Task Remove(string id, string userId)
        {
            await using var context = _contextFactory.CreateContext();
            var device = await context.Fido2Credentials.FindAsync(id);
            if (device == null || !device.ApplicationUserId.Equals(userId, StringComparison.InvariantCulture))
            {
                return;
            }

            context.Fido2Credentials.Remove(device);
            await context.SaveChangesAsync();
        }

        public async Task<bool> HasCredentials(string userId)
        {
            await using var context = _contextFactory.CreateContext();
            return await context.Fido2Credentials.Where(fDevice => fDevice.ApplicationUserId == userId).AnyAsync();
        }

    }
}

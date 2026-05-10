using DiplomMurtazin.Core;
using System;

namespace DiplomMurtazin.Model
{
    public static class DataContext
    {
        private static KPMurtazinEntities _context;

        public static KPMurtazinEntities GetContext()
        {
            if (_context == null)
            {
                try
                {
                    _context = new KPMurtazinEntities();
                    _context.Configuration.ProxyCreationEnabled = false;
                    _context.Configuration.LazyLoadingEnabled = false;
                }
                catch
                {
                    return null;
                }
            }
            return _context;
        }

        public static bool TestConnection()
        {
            try
            {
                return ConnectionManager.TestServerConnection(out _);
            }
            catch
            {
                return false;
            }
        }
    }
}
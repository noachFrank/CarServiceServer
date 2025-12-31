using DispatchApp.Server.Data.DataTypes;
using DispatchApp.Server.data;
using Microsoft.EntityFrameworkCore;

namespace DispatchApp.Server.Data.DataRepositories
{
    public class CommunicationRepo
    {
        private string _connectionString;

        public CommunicationRepo(string connectionString)
        {
            _connectionString = connectionString;
        }
        public void AddCom(Communication com)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                context.Communications.Add(com);
                context.SaveChanges();
            }
        }

        public List<Communication> GetTodaysCom(int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Communications.Where(x => x.DriverId == driverId && x.Date.Date >= DateTime.Today).ToList();
            }
        }

        public List<Communication> GetAllCom(int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Communications.Where(x => x.DriverId == driverId).ToList();
            }
        }

        public List<Communication> GetBroadcastComs()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Communications.Where(x => x.From.ToLower() == "broadcast").ToList();
            }
        }

        public List<Communication> GetUnread()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Communications
                    .Include(c => c.Driver)
                    .Where(x => x.From != null && x.From.ToLower() == "driver" && (x.Read == null || !x.Read))
                    .ToList();
            }
        }

        public List<Communication> GetDriverUnread(int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Communications.Where(x => x.DriverId == driverId && x.From.ToLower() != "driver" && (x.Read == null || !x.Read)).ToList();
            }
        }

        public void MarkAsRead(int comId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var com = context.Communications.FirstOrDefault(x => x.Id == comId);
                if (com != null)
                {
                    com.Read = true;
                    context.SaveChanges();
                }
            }
        }

        public Communication GetById(int comId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Communications.FirstOrDefault(x => x.Id == comId);
            }
        }

    }
}

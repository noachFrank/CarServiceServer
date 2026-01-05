using DispatchApp.Server.data;
using DispatchApp.Server.Data.DataTypes;
using DispatchApp.Server.Utils;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace DispatchApp.Server.Data.DataRepositories
{
    public class RideRepo
    {
        private string _connectionString;

        public RideRepo(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region get rides
        public List<Ride> GetAllRides()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides.Include(r => r.Route).Include(r => r.AssignedTo).Include(r => r.ReassignedTo).Include(r => r.Recurring).ToList();
            }
        }
        public List<Ride> GetAllRidesThisWeek()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return GetAllRides().Where(x => x.ScheduledFor.IsInThisWeek()).ToList();
            }
        }

        public List<Ride> GetAssignedRides()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides.Include(r => r.Route).Include(r => r.AssignedTo).Include(r => r.ReassignedTo).Include(r => r.Recurring)
                    .Where(x => (x.Reassigned != null && (!x.Reassigned && x.AssignedToId != null) || x.ReassignedToId != null)
                    && x.PickupTime == null && !x.Canceled).ToList();
            }
        }

        public List<Ride> GetRidesInProgress()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides.Include(r => r.Route).Include(r => r.AssignedTo).Include(r => r.ReassignedTo).Include(r => r.Recurring)
                    .Where(x => x.PickupTime != null && x.DropOffTime == null && !x.Canceled).ToList();
            }
        }

        public List<Ride> GetRecurringRidesThisWeek()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                var endOfWeek = startOfWeek.AddDays(7);

                return context.Rides.Include(r => r.Route).Include(r => r.AssignedTo).Include(r => r.ReassignedTo).Include(r => r.Recurring)
                    .Where(x => x.IsRecurring && x.ScheduledFor >= startOfWeek && x.ScheduledFor < endOfWeek && !x.Canceled).ToList();
            }
        }

        public List<Ride> GetUpcomingRidesByDriver(int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides.Include(r => r.Route).Include(r => r.AssignedTo).Include(r => r.ReassignedTo).Include(r => r.Recurring)
                    .Where(x => ((x.Reassigned && x.ReassignedToId == driverId) || (!x.Reassigned && x.AssignedToId == driverId))
                    && x.DropOffTime == null && !x.Canceled).ToList();
            }
        }

        public List<Ride> GetRidesCurrentlyBeingDriven()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides.Include(r => r.Route).Include(r => r.AssignedTo).Include(r => r.ReassignedTo).Include(r => r.Recurring)
                    .Where(x => !x.Canceled && (x.AssignedTo != null || (x.Reassigned && x.ReassignedToId != null)) && x.DropOffTime == null && (x.ScheduledFor <= DateTime.Now || x.PickupTime != null)).ToList();
            }
        }

        public List<Ride> GetOpenRidesNotAssigned()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides.Include(r => r.Route).Include(r => r.AssignedTo).Include(r => r.ReassignedTo).Include(r => r.Recurring)
                    .Where(x => (x.AssignedToId == null && (x.Reassigned == null || !x.Reassigned))
                    || (x.Reassigned != null && x.Reassigned && x.ReassignedToId == null))
                    .Where(x => !x.Canceled).ToList();
            }
        }

        public List<Ride> GetOpenRidesNotAssigned(int userId)
        {
            var userRepo = new UserRepo(_connectionString);
            var car = userRepo.GetPrimaryCar(userId);
            if (car == null)
                return null;

            using (var context = new DispatchDbContext(_connectionString))
            {
                return GetOpenRidesNotAssigned().Where(x => (x.Passengers <= car.Seats) && ((x.CarType == CarType.LuxurySUV && car.Type == CarType.LuxurySUV) || x.CarType <= car.Type)).ToList();
            }
        }

        public List<Ride> GetFutureRides()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides.Include(r => r.Route).Include(r => r.AssignedTo).Include(r => r.ReassignedTo).Include(r => r.Recurring)
                    .Where(x => x.AssignedTo == null && x.ScheduledFor >= DateTime.Now && !x.Canceled).ToList();
            }
        }

        public List<Ride> GetTodaysRides()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides.Include(r => r.Route).Include(r => r.AssignedTo).Include(r => r.ReassignedTo).Include(r => r.Recurring)
                    .Where(x => x.ScheduledFor.Date == DateTime.Today.Date).ToList();
            }
        }

        public List<Ride> GetCompletedRides()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides.Include(r => r.Route).Include(r => r.AssignedTo).Include(r => r.ReassignedTo).Include(r => r.Recurring)
                    .Where(x => x.DropOffTime != null).ToList();
            }
        }

        public List<Ride> GetCompletedRides(DateTime date)
        {
            return GetCompletedRides().Where(x => x.DropOffTime != null && x.DropOffTime.Value.Date == date.Date).ToList();
        }

        public List<Ride> GetCompletedRides(int driverId)
        {
            return GetCompletedRides().Where(x => x.DropOffTime != null && ((x.Reassigned && x.ReassignedToId != null && x.ReassignedToId == driverId) ||
                                                    (!x.Reassigned && x.AssignedToId != null && x.AssignedToId == driverId))).ToList();
        }

        public List<Ride> GetCompletedRides(int driverId, DateTime date)
        {
            return GetCompletedRides(driverId).Where(x => x.DropOffTime.Value.Date == date.Date).ToList();
        }

        public List<Ride> GetDispatchedRides(DateTime date)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides.Include(r => r.Route).Include(r => r.AssignedTo).Include(r => r.ReassignedTo).Include(r => r.Recurring)
                    .Where(x => x.DropOffTime != null && x.DropOffTime.Value.Date == date.Date).ToList();
            }
        }

        public List<Ride> GetDispatchedRides(int dispatcherId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides.Include(r => r.Route).Include(r => r.Recurring).Where(x => x.DropOffTime != null && x.DispatchedById != null && x.DispatchedById == dispatcherId).ToList();
            }
        }

        public List<Ride> GetDispatchedRides(int dispatcherId, DateTime date)
        {
            return GetDispatchedRides(dispatcherId).Where(x => x.DropOffTime.Value.Date == date.Date).ToList();
        }

        public Ride GetById(int rideId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides
                    .Include(r => r.Route)
                    .Include(r => r.AssignedTo)
                    .Include(r => r.ReassignedTo)
                    .Include(r => r.DispatchedBy)
                    .Include(r => r.Recurring)
                    .Include(r => r.AssignedTo.Cars)
                    .Include(r => r.ReassignedTo.Cars)
                    .FirstOrDefault(x => x.RideId == rideId);
            }
        }

        public List<Ride> GetUnsettledRides()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides
                    .Include(r => r.Route)
                    .Include(r => r.AssignedTo)
                    .Include(r => r.ReassignedTo)
                    .Where(r => !r.Settled && r.DropOffTime != null && !r.Canceled)
                    .ToList();
            }
        }

        public List<Ride> GetUnsettledRidesByDriver(int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Rides
                    .Include(r => r.Route)
                    .Include(r => r.AssignedTo)
                    .Include(r => r.ReassignedTo)
                    .Where(r => !r.Settled && r.DropOffTime != null && !r.Canceled &&
                        ((r.Reassigned && r.ReassignedToId == driverId) || (!r.Reassigned && r.AssignedToId == driverId)))
                    .ToList();
            }
        }

        #endregion

        #region add/edit rides
        public void AddRide(Ride ride)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                if (ride.Route != null)
                {
                    context.Routes.Add(ride.Route);
                    context.SaveChanges();
                    ride.RouteId = ride.Route.RouteId;
                }

                context.Rides.Add(ride);
                context.SaveChanges();
            }
        }

        public void AddRecurring(Recurring reccurring)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                context.Recurrings.Add(reccurring);
                context.SaveChanges();
            }
        }

        public void AssignRide(int rideId, int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(x => x.RideId == rideId);

                if (ride == null)
                    throw new Exception($"Ride with ID {rideId} not found.");
                if (ride.AssignedToId != null && ride.Reassigned)
                    ReassignRide(rideId, driverId);
                ride.AssignedToId = driverId;
                context.SaveChanges();
                var userRepo = new UserRepo(_connectionString);
                userRepo.ChangeDriverOnJobStatus(driverId, true);
            }
        }

        public void ReassignRide(int rideId, int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(x => x.RideId == rideId);

                if (ride == null)
                    throw new Exception($"Ride with ID {rideId} not found.");
                else if (ride.AssignedToId == null)
                    AssignRide(rideId, driverId);

                ride.ReassignedToId = driverId;
                context.SaveChanges();
                var userRepo = new UserRepo(_connectionString);
                userRepo.ChangeDriverOnJobStatus(driverId, true);
            }
        }

        public void MarkRidePickedUp(int rideId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(x => x.RideId == rideId);

                if (ride == null)
                    throw new Exception($"Ride with ID {rideId} not found.");

                ride.PickupTime = DateTime.UtcNow;
                context.SaveChanges();
            }
        }

        public void MarkRideDroppedOff(int rideId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(x => x.RideId == rideId);

                if (ride == null)
                    throw new Exception($"Ride with ID {rideId} not found.");


                if (ride.IsRecurring && ride.RecurringId is int recId)
                {
                    var recurring = context.Recurrings.FirstOrDefault(r => r.Id == recId);
                    if (recurring != null && recurring.EndDate.Date >= ride.ScheduledFor.Date)
                    {
                        var newRide = new Ride
                        {
                            RouteId = ride.RouteId,
                            CustomerName = ride.CustomerName,
                            CustomerPhoneNumber = ride.CustomerPhoneNumber,
                            CallTime = ride.CallTime,
                            ScheduledFor = ride.ScheduledFor.AddDays(7),
                            Cost = ride.Cost - (ride.Tip + ride.WaitTimeAmount),
                            DriversCompensation = ride.DriversCompensation - (ride.Tip + ride.WaitTimeAmount),
                            AssignedToId = ride.Reassigned ? ride.ReassignedToId : ride.AssignedToId,
                            Notes = ride.Notes,
                            DispatchedById = ride.DispatchedById,
                            PaymentType = ride.PaymentType,
                            CarType = ride.CarType,
                            Passengers = ride.Passengers,
                            CarSeat = ride.CarSeat,
                            IsRecurring = true,
                            RecurringId = ride.RecurringId,
                            Route = ride.Route
                        };
                        AddRide(newRide);
                    }
                }

                ride.DropOffTime = DateTime.UtcNow;
                context.SaveChanges();

            }
        }

        public void CancelDriver(int rideId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(x => x.RideId == rideId);

                if (ride == null)
                    throw new Exception($"Ride with ID {rideId} not found.");

                if (ride.Reassigned != null && ride.Reassigned)
                    ride.ReassignedToId = null;
                ride.Reassigned = true;
                context.SaveChanges();
            }
        }

        public void CancelRide(int rideId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(x => x.RideId == rideId);

                if (ride == null)
                    throw new Exception($"Ride with ID {rideId} not found.");

                if (ride.IsRecurring && ride.RecurringId is int recId)
                {
                    var recurring = context.Recurrings.FirstOrDefault(r => r.Id == recId);
                    if (recurring != null && recurring.EndDate.Date >= ride.ScheduledFor.Date)
                    {
                        var newRide = new Ride
                        {
                            RouteId = ride.RouteId,
                            CustomerName = ride.CustomerName,
                            CustomerPhoneNumber = ride.CustomerPhoneNumber,
                            CallTime = ride.CallTime,
                            ScheduledFor = ride.ScheduledFor.AddDays(7),
                            Cost = ride.Cost - (ride.Tip + ride.WaitTimeAmount),
                            DriversCompensation = ride.DriversCompensation - (ride.Tip + ride.WaitTimeAmount),
                            AssignedToId = ride.Reassigned ? ride.ReassignedToId : ride.AssignedToId,
                            Notes = ride.Notes,
                            DispatchedById = ride.DispatchedById,
                            PaymentType = ride.PaymentType,
                            CarType = ride.CarType,
                            Passengers = ride.Passengers,
                            CarSeat = ride.CarSeat,
                            IsRecurring = true,
                            RecurringId = ride.RecurringId,
                            Route = ride.Route,
                        };
                        AddRide(newRide);
                    }
                }

                ride.Canceled = true;
                context.SaveChanges();
            }
        }

        public void AddStops(int rideId, string stop)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var route = context.Rides.Include(r => r.Route).FirstOrDefault(x => x.RideId == rideId).Route;

                if (route == null)
                    throw new Exception($"Ride with ID {rideId} not found.");

                var stopCount = GetLastStopIndex(route);
                if (stopCount == 10)
                    throw new Exception();
                else if (stopCount == 9)
                    route.Stop10 = stop;
                else if (stopCount == 8)
                    route.Stop9 = stop;
                else if (stopCount == 7)
                    route.Stop8 = stop;
                else if (stopCount == 6)
                    route.Stop7 = stop;
                else if (stopCount == 5)
                    route.Stop6 = stop;
                else if (stopCount == 4)
                    route.Stop5 = stop;
                else if (stopCount == 3)
                    route.Stop4 = stop;
                else if (stopCount == 2)
                    route.Stop3 = stop;
                else if (stopCount == 1)
                    route.Stop2 = stop;
                else if (stopCount == 0)
                    route.Stop1 = stop;

                context.SaveChanges();
            }
        }

        public void AddToPrice(int rideId, decimal cost, decimal driversComp)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(x => x.RideId == rideId);

                if (ride == null)
                    throw new Exception($"Ride with ID {rideId} not found.");

                ride.Cost += cost;
                ride.DriversCompensation += driversComp;

                context.SaveChanges();
            }
        }

        public void ChangePrice(int rideId, decimal cost, decimal driversComp)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(x => x.RideId == rideId);

                if (ride == null)
                    throw new Exception($"Ride with ID {rideId} not found.");

                ride.Cost = cost;
                ride.DriversCompensation = driversComp;

                context.SaveChanges();
            }
        }

        public void AddWaitTimeAmount(int rideId, decimal amount)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(x => x.RideId == rideId);

                if (ride == null)
                    throw new Exception($"Ride with ID {rideId} not found.");

                ride.WaitTimeAmount = amount;
                context.SaveChanges();
                AddToPrice(rideId, amount, amount * 0.85m);

            }
        }

        public void AddTip(int rideId, decimal amount)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(x => x.RideId == rideId);

                if (ride == null)
                    throw new Exception($"Ride with ID {rideId} not found.");

                ride.Tip = amount;
                context.SaveChanges();
                AddToPrice(rideId, amount, amount);
            }
        }

        public void ResetPickupTime(int rideId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(x => x.RideId == rideId);

                if (ride == null)
                    throw new Exception($"Ride with ID {rideId} not found.");

                ride.PickupTime = null;

                context.SaveChanges();
            }
        }

        public void CancelRecurring(int rideId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var recRide = context.Rides.FirstOrDefault(x => x.RideId == rideId);
                if (recRide?.IsRecurring == null)
                    return;
                var recurring = context.Recurrings.FirstOrDefault(r => r.Id == recRide.RecurringId);
                if (recurring != null)
                    recurring.EndDate = DateTime.UtcNow;

                recRide.IsRecurring = false;
                context.SaveChanges();
            }
        }



        public void SettleDriverRides(int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var rides = context.Rides
                    .Where(r => !r.Settled && r.DropOffTime != null && !r.Canceled &&
                        ((r.Reassigned && r.ReassignedToId == driverId) || (!r.Reassigned && r.AssignedToId == driverId)))
                    .ToList();

                foreach (var ride in rides)
                {
                    ride.Settled = true;
                }

                context.SaveChanges();
            }
        }

        public void SettleRide(int rideId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var ride = context.Rides.FirstOrDefault(r => r.RideId == rideId);
                if (ride != null)
                {
                    ride.Settled = true;
                    context.SaveChanges();
                }
            }
        }


        public int GetLastStopIndex(Routes route)
        {
            return new List<string>()
            {
                route.Stop1,
                route.Stop2,
                route.Stop3,
                route.Stop4,
                route.Stop5,
                route.Stop6,
                route.Stop7,
                route.Stop8,
                route.Stop9,
                route.Stop10

            }.Count(s => !string.IsNullOrEmpty(s));
        }

        #endregion
    }
}

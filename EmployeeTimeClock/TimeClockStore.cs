using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace EmployeeTimeClock
{
    // keep loaded timeclock records in memory while MariaDB provides the saved source of truth -H
    internal static class TimeClockStore
    {
        private static readonly object TimeSyncLock = new object();
        private static readonly DateTime NtpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static DateTime synchronizedUtc;
        private static long synchronizedAtTick;
        private static DateTime lastSyncAttemptUtc = DateTime.MinValue;
        private static bool hasNtpTime;
        private static bool syncInProgress;
        private static string timeSyncStatus = "NTP sync has not been attempted";
        private const int NtpRefreshMinutes = 15;

        // return the NTP-adjusted local time and refresh the offset periodically -H
        internal static DateTime Now
        {
            get
            {
                if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return DateTime.Now;
                bool shouldRefresh;
                lock (TimeSyncLock)
                {
                    double elapsedSinceSync = hasNtpTime
                        ? (Stopwatch.GetTimestamp() - synchronizedAtTick) / (double)Stopwatch.Frequency
                        : Double.MaxValue;
                    shouldRefresh = !syncInProgress && hasNtpTime && elapsedSinceSync >= NtpRefreshMinutes * 60;
                }
                if (shouldRefresh) SynchronizeTime();
                lock (TimeSyncLock)
                {
                    // use a monotonic timer so local computer clock changes do not shift NTP time -H
                    if (hasNtpTime)
                    {
                        double elapsedSeconds = (Stopwatch.GetTimestamp() - synchronizedAtTick) / (double)Stopwatch.Frequency;
                        return synchronizedUtc.AddSeconds(elapsedSeconds).ToLocalTime();
                    }
                    // keep the proof-of-concept usable offline and expose that it is using the local clock -H
                    return DateTime.Now;
                }
            }
        }

        // report whether punches are currently based on NTP or the computer's local clock -H
        internal static string ClockTimeStatus
        {
            get { lock (TimeSyncLock) return hasNtpTime ? timeSyncStatus : "Local clock (NTP unavailable)"; }
        }

        // keep NTP details in Visual Studio's Debug Output instead of employee and manager status labels -H
        internal static string FormatStatus(string status)
        {
            return status;
        }

        // query a standard NTP server and save the measured UTC offset for application time -H
        internal static bool SynchronizeTime()
        {
            lock (TimeSyncLock)
            {
                if (syncInProgress) return hasNtpTime;
                syncInProgress = true;
                lastSyncAttemptUtc = DateTime.UtcNow;
            }
            try
            {
                // try two independent public time hosts so a single unavailable server does not stop sync -H
                string[] servers = { "time.windows.com", "pool.ntp.org" };
                foreach (string server in servers)
                {
                    DateTime adjustedUtc;
                    if (!TryGetNtpTime(server, out adjustedUtc)) continue;
                    lock (TimeSyncLock)
                    {
                        synchronizedUtc = adjustedUtc;
                        synchronizedAtTick = Stopwatch.GetTimestamp();
                        hasNtpTime = true;
                        timeSyncStatus = "NTP synchronized";
                    }
                    Debug.WriteLine("NTP synchronized using " + server);
                    return true;
                }
                bool retainedNtpTime;
                lock (TimeSyncLock)
                {
                    timeSyncStatus = hasNtpTime ? "Last NTP sync retained" : "Local clock (NTP unavailable)";
                    retainedNtpTime = hasNtpTime;
                }
                Debug.WriteLine(retainedNtpTime ? "NTP refresh failed; retaining the last network time" : "NTP unavailable; using the computer's local clock");
                return false;
            }
            finally
            {
                lock (TimeSyncLock) syncInProgress = false;
            }
        }

        // estimate server clock offset from the four timestamps in one NTP exchange -H
        private static bool TryGetNtpTime(string server, out DateTime adjustedUtc)
        {
            adjustedUtc = DateTime.MinValue;
            try
            {
                byte[] request = new byte[48];
                request[0] = 0x23;
                DateTime sentUtc = DateTime.UtcNow;
                WriteNtpTimestamp(request, 40, sentUtc);
                using (UdpClient client = new UdpClient())
                {
                    client.Client.SendTimeout = 2000;
                    client.Client.ReceiveTimeout = 2000;
                    client.Connect(server, 123);
                    client.Send(request, request.Length);
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] response = client.Receive(ref remote);
                    DateTime receivedUtc = DateTime.UtcNow;
                    // accept only a valid server response to this request -H
                    if (response.Length < 48 || (response[0] & 7) != 4 || response[1] == 0 || response[1] > 15 || (response[0] >> 6) == 3)
                        return false;
                    for (int index = 0; index < 8; index++)
                        if (response[24 + index] != request[40 + index]) return false;
                    DateTime serverReceivedUtc = ReadNtpTimestamp(response, 32, receivedUtc);
                    DateTime serverSentUtc = ReadNtpTimestamp(response, 40, receivedUtc);
                    // apply the RFC four-timestamp offset estimate to the receive time -H
                    double offsetSeconds = ((serverReceivedUtc - sentUtc).TotalSeconds + (serverSentUtc - receivedUtc).TotalSeconds) / 2.0;
                    adjustedUtc = receivedUtc.AddSeconds(offsetSeconds);
                    return true;
                }
            }
            catch (SocketException) { return false; }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        // encode UTC seconds and fractional seconds into the NTP packet format -H
        private static void WriteNtpTimestamp(byte[] packet, int offset, DateTime utc)
        {
            ulong seconds = (ulong)(utc - NtpEpoch).Ticks / (ulong)TimeSpan.TicksPerSecond;
            ulong fraction = ((ulong)((utc - NtpEpoch).Ticks % TimeSpan.TicksPerSecond) << 32) / (ulong)TimeSpan.TicksPerSecond;
            WriteNetworkUInt32(packet, offset, (uint)seconds);
            WriteNetworkUInt32(packet, offset + 4, (uint)fraction);
        }

        // decode NTP's wrapped seconds field using the closest era to the local UTC date -H
        private static DateTime ReadNtpTimestamp(byte[] packet, int offset, DateTime referenceUtc)
        {
            ulong seconds = ReadNetworkUInt32(packet, offset);
            ulong fraction = ReadNetworkUInt32(packet, offset + 4);
            const long secondsPerEra = 1L << 32;
            long currentEra = (long)Math.Floor((referenceUtc - NtpEpoch).TotalSeconds / secondsPerEra);
            long nearestSeconds = (long)seconds + currentEra * secondsPerEra;
            long previousEraSeconds = nearestSeconds - secondsPerEra;
            long nextEraSeconds = nearestSeconds + secondsPerEra;
            if (Math.Abs(previousEraSeconds - (referenceUtc - NtpEpoch).TotalSeconds) < Math.Abs(nearestSeconds - (referenceUtc - NtpEpoch).TotalSeconds)) nearestSeconds = previousEraSeconds;
            if (Math.Abs(nextEraSeconds - (referenceUtc - NtpEpoch).TotalSeconds) < Math.Abs(nearestSeconds - (referenceUtc - NtpEpoch).TotalSeconds)) nearestSeconds = nextEraSeconds;
            double fractionalSeconds = fraction / 4294967296.0;
            return NtpEpoch.AddSeconds(nearestSeconds + fractionalSeconds);
        }

        // read or write one unsigned 32-bit value in network byte order -H
        private static uint ReadNetworkUInt32(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private static void WriteNetworkUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        // these small records keep the in-memory proof of concept shaped like future database tables -H
        internal sealed class User
        {
            public int Id;
            public string Username;
            public string PasswordHash;
            public string Name;
            public bool IsActive;
            public bool IsManager;
            public decimal HourlyRate;
        }

        internal sealed class WorkPeriod
        {
            public int Id;
            // each break is stored separately so multiple breaks can be subtracted from a shift -H
            public sealed class BreakPeriod
            {
                public DateTime Start;
                public DateTime? End;
            }

            public int EmployeeId;
            public DateTime ClockIn;
            public DateTime? ClockOut;
            public DateTime? BreakStart;
            public DateTime? BreakEnd;
            public readonly List<BreakPeriod> Breaks = new List<BreakPeriod>();
            public double HoursWorked
            {
                get
                {
                    // an unfinished shift has no final total; otherwise subtract all completed breaks -H
                    if (!ClockOut.HasValue) return 0;
                    double hours = (ClockOut.Value - ClockIn).TotalHours;
                    hours -= Breaks.Where(item => item.End.HasValue).Sum(item => (item.End.Value - item.Start).TotalHours);
                    if (Breaks.Count == 0 && BreakStart.HasValue && BreakEnd.HasValue) hours -= (BreakEnd.Value - BreakStart.Value).TotalHours;
                    return Math.Max(0, hours);
                }
            }
        }

        internal sealed class TimeOffRequest
        {
            public int Id;
            // one record represents one requested date and its hours, reason, and submission time -H
            public int EmployeeId;
            public DateTime Day;
            public int Hours;
            public string Reason;
            public DateTime Submitted;
            // retain the manager decision and reviewer for the PTO review screen -H
            public string Status = "Pending";
            public int? ReviewedBy;
            public DateTime? ReviewedAt;
        }

        internal sealed class Availability
        {
            // weekly availability is stored as one weekday and a start/end time window -H
            public int EmployeeId;
            public DayOfWeek Day;
            public TimeSpan Start;
            public TimeSpan End;
        }

        internal sealed class Shift
        {
            public int Id;
            // generated and edited schedule entries keep the employee ID and full date/time bounds -H
            public int EmployeeId;
            public DateTime Start;
            public DateTime End;
        }

        internal sealed class ScheduleDayPreference
        {
            // reusable weekday defaults describe opening hours and simultaneous minimum staffing -H
            public DayOfWeek Day;
            public bool IsOpen;
            public TimeSpan OpeningTime;
            public TimeSpan ClosingTime;
            public int MinimumStaff;
        }

        internal sealed class AttendancePoint
        {
            public long Id;
            // retain the reason and related shift so repeated checks do not award duplicate points -H
            public int EmployeeId;
            public DateTime ShiftStart;
            public DateTime RecordedAt;
            public decimal Points;
            public string Reason;
            public bool IsReversed;
        }

        internal sealed class AuditEntry
        {
            // record who changed a timesheet, when, and the manager's reason for the edit -H
            public int EmployeeId;
            public int ManagerId;
            public DateTime ChangedAt;
            public string Reason;
            public string Change;
            public long RecordId;
            public string OldValues;
            public string NewValues;
        }

        // keep database-loaded accounts available to every role-based screen -H
        internal static readonly List<User> Users = new List<User>();
        internal static readonly List<WorkPeriod> WorkPeriods = new List<WorkPeriod>();
        internal static readonly List<TimeOffRequest> TimeOffRequests = new List<TimeOffRequest>();
        internal static readonly List<Availability> Availabilities = new List<Availability>();
        internal static readonly List<Shift> Shifts = new List<Shift>();
        // editable example hours provide a usable starting schedule, not assumed business policy -H
        internal static readonly List<ScheduleDayPreference> SchedulePreferences = new List<ScheduleDayPreference>
        {
            new ScheduleDayPreference { Day = DayOfWeek.Sunday, IsOpen = true, OpeningTime = TimeSpan.FromHours(9), ClosingTime = TimeSpan.FromHours(21), MinimumStaff = 1 },
            new ScheduleDayPreference { Day = DayOfWeek.Monday, IsOpen = true, OpeningTime = TimeSpan.FromHours(9), ClosingTime = TimeSpan.FromHours(21), MinimumStaff = 1 },
            new ScheduleDayPreference { Day = DayOfWeek.Tuesday, IsOpen = true, OpeningTime = TimeSpan.FromHours(9), ClosingTime = TimeSpan.FromHours(21), MinimumStaff = 1 },
            new ScheduleDayPreference { Day = DayOfWeek.Wednesday, IsOpen = true, OpeningTime = TimeSpan.FromHours(9), ClosingTime = TimeSpan.FromHours(21), MinimumStaff = 1 },
            new ScheduleDayPreference { Day = DayOfWeek.Thursday, IsOpen = true, OpeningTime = TimeSpan.FromHours(9), ClosingTime = TimeSpan.FromHours(21), MinimumStaff = 1 },
            new ScheduleDayPreference { Day = DayOfWeek.Friday, IsOpen = true, OpeningTime = TimeSpan.FromHours(9), ClosingTime = TimeSpan.FromHours(21), MinimumStaff = 1 },
            new ScheduleDayPreference { Day = DayOfWeek.Saturday, IsOpen = true, OpeningTime = TimeSpan.FromHours(9), ClosingTime = TimeSpan.FromHours(21), MinimumStaff = 1 }
        };
        // keep per-week exceptions separately so events do not rewrite normal weekday preferences -H
        internal static readonly Dictionary<DateTime, string[]> ScheduleWeekEdits = new Dictionary<DateTime, string[]>();
        // prep/cleanup buffers are editable alongside the weekday preference table -H
        internal static int SetupMinutes = 15;
        internal static int CleanupMinutes = 15;
        internal static readonly List<AttendancePoint> AttendancePoints = new List<AttendancePoint>();
        internal static readonly List<AuditEntry> Audit = new List<AuditEntry>();

        // Shared limits keep employee and manager timeclock screens on the same attendance rules -H
        internal const int ClockGraceMinutes = 15;
        internal const int MinimumBreakMinutes = 30;
        internal const int MinimumWorkBeforeBreakHours = 3;
        // keep generated and manually edited shifts under the same daily duration limit -H
        internal const double MaximumShiftHours = 8.0;

        // supplies manager screens with the employee accounts in the local store -H
        internal static IEnumerable<User> Employees { get { return Users.Where(user => user.IsActive && !user.IsManager); } }

        // finds an account by its ID for manager workflows -H
        internal static User FindUser(int id)
        {
            return Users.FirstOrDefault(user => user.Id == id);
        }

        // records an attendance point once and writes it to MariaDB before showing it in memory -H
        internal static void AddAttendancePoint(int employeeId, DateTime shiftStart, decimal points, string reason)
        {
            // identify a point by employee, shift, and reason before adding it to avoid duplicate events -H
            bool alreadyRecorded = AttendancePoints.Any(item => item.EmployeeId == employeeId && item.ShiftStart == shiftStart && item.Reason == reason);
            if (alreadyRecorded) return;
            AttendancePoint point = new AttendancePoint { EmployeeId = employeeId, ShiftStart = shiftStart, RecordedAt = Now, Points = points, Reason = reason };
            TimeClockDatabase.SaveAttendancePoint(point);
            AttendancePoints.Add(point);
        }

        // reverse an inferred no-show if the employee later clocks in for that shift -H
        internal static void ReverseNoShow(int employeeId, DateTime shiftStart)
        {
            TimeClockDatabase.ReverseNoShow(employeeId, shiftStart, Now);
            foreach (AttendancePoint point in AttendancePoints.Where(item => item.EmployeeId == employeeId && item.ShiftStart == shiftStart && item.Reason == "No show"))
                point.IsReversed = true;
            AttendancePoints.RemoveAll(item => item.EmployeeId == employeeId && item.ShiftStart == shiftStart && item.Reason == "No show");
        }

        // reverse an active attendance reason while retaining its database history -H
        internal static void ReverseAttendancePoint(int employeeId, DateTime shiftStart, string reason)
        {
            TimeClockDatabase.ReverseAttendancePoint(employeeId, shiftStart, reason, Now);
            AttendancePoints.RemoveAll(item => item.EmployeeId == employeeId && item.ShiftStart == shiftStart && item.Reason == reason);
        }

        // align points with a manager-corrected punch and its saved scheduled shift -H
        internal static void ReconcileShiftAttendance(int employeeId, Shift shift, DateTime clockIn, DateTime? clockOut)
        {
            if (shift == null) return;
            // a saved clock-in means this shift should no longer retain an inferred no-show -H
            ReverseNoShow(employeeId, shift.Start);
            if (clockIn > shift.Start.AddMinutes(ClockGraceMinutes)) AddAttendancePoint(employeeId, shift.Start, 0.5m, "Late arrival");
            else ReverseAttendancePoint(employeeId, shift.Start, "Late arrival");
            if (clockOut < shift.End.AddMinutes(-ClockGraceMinutes)) AddAttendancePoint(employeeId, shift.Start, 0.5m, "Left early");
            else ReverseAttendancePoint(employeeId, shift.Start, "Left early");
            if (clockOut > shift.End.AddMinutes(ClockGraceMinutes)) AddAttendancePoint(employeeId, shift.Start, 0.5m, "Worked past scheduled end");
            else ReverseAttendancePoint(employeeId, shift.Start, "Worked past scheduled end");
        }
        // load persisted users and timeclock records before opening the login screen -H
        internal static void LoadFromDatabase()
        {
            TimeClockDatabase.LoadAll();
        }

        // hashes a login attempt to compare it with the SHA-256 values stored in MariaDB -H
        private static string HashPassword(string password)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(password))).Replace("-", "").ToLowerInvariant();
        }

        // validates a login against active database-loaded accounts -H
        internal static User Authenticate(string username, string password)
        {
            string passwordHash = HashPassword(password);
            return Users.FirstOrDefault(user => user.IsActive && String.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase) && String.Equals(user.PasswordHash, passwordHash, StringComparison.OrdinalIgnoreCase));
        }

        // returns the active work period, if the employee is clocked in -H
        internal static WorkPeriod ActivePeriod(int employeeId)
        {
            return WorkPeriods.LastOrDefault(period => period.EmployeeId == employeeId && !period.ClockOut.HasValue);
        }

        // Find the scheduled shift that controls this employee's punches on the selected date -H
        internal static Shift ScheduledShiftForDate(int employeeId, DateTime date)
        {
            return Shifts.Where(shift => shift.EmployeeId == employeeId && shift.Start.Date == date.Date).OrderBy(shift => shift.Start).FirstOrDefault();
        }

        // returns completed and active work periods for a timesheet -H
        internal static IEnumerable<WorkPeriod> Timesheet(int employeeId, DateTime from, DateTime through)
        {
            return WorkPeriods.Where(period => period.EmployeeId == employeeId && period.ClockIn.Date >= from.Date && period.ClockIn.Date <= through.Date).OrderBy(period => period.ClockIn);
        }

        // calculates recorded hours in a date range -H
        internal static double TotalHours(int employeeId, DateTime from, DateTime through)
        {
            return Timesheet(employeeId, from, through).Sum(period => period.HoursWorked);
        }
    }
}

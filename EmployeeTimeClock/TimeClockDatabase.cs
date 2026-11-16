using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MySqlConnector;

namespace EmployeeTimeClock
{
    // load and save timeclock records through the configured MariaDB connection -H
    internal static class TimeClockDatabase
    {
        // build one short-timeout connection string from DatabaseConfig.cs -H
        private static string ConnectionString
        {
            get
            {
                if (String.IsNullOrWhiteSpace(DatabaseConfig.Server) || String.IsNullOrWhiteSpace(DatabaseConfig.Username) ||
                    String.IsNullOrWhiteSpace(DatabaseConfig.DatabaseName) || String.IsNullOrWhiteSpace(DatabaseConfig.Password))
                    throw new InvalidOperationException("Fill in the MariaDB connection settings in DatabaseConfig.cs first");
                if (DatabaseConfig.Port < 1 || DatabaseConfig.Port > 65535)
                    throw new InvalidOperationException("The MariaDB port in DatabaseConfig.cs must be between 1 and 65535");
                return new MySqlConnectionStringBuilder
                {
                    Server = DatabaseConfig.Server,
                    Port = (uint)DatabaseConfig.Port,
                    UserID = DatabaseConfig.Username,
                    Password = DatabaseConfig.Password,
                    Database = DatabaseConfig.DatabaseName,
                    ConnectionTimeout = 8,
                    DefaultCommandTimeout = 30,
                    Pooling = true,
                    CharacterSet = "utf8mb4"
                }.ConnectionString;
            }
        }

        // open one authenticated connection for a database operation -H
        private static MySqlConnection OpenConnection()
        {
            MySqlConnection connection = new MySqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        // load persisted records before the login screen is displayed -H
        internal static void LoadAll()
        {
            using (MySqlConnection connection = OpenConnection())
            {
                LoadUsers(connection);
                LoadWorkPeriods(connection);
                LoadShifts(connection);
                LoadPtoRequests(connection);
                LoadAvailability(connection);
                LoadAttendancePoints(connection);
                LoadAudit(connection);
                LoadSchedulePreferences(connection);
                LoadScheduleWeekSettings(connection);
                LoadScheduleGenerationSettings(connection);
            }
        }

        // map database accounts into the employee and manager selectors -H
        private static void LoadUsers(MySqlConnection connection)
        {
            TimeClockStore.Users.Clear();
            const string sql = "SELECT id, username, password_hash, display_name, role, pay_rate, is_active FROM `440_timeclock_users`";
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    TimeClockStore.Users.Add(new TimeClockStore.User
                    {
                        Id = reader.GetInt32("id"), Username = reader.GetString("username"),
                        PasswordHash = reader.GetString("password_hash").Trim(), Name = reader.GetString("display_name"),
                        IsManager = String.Equals(reader.GetString("role"), "manager", StringComparison.OrdinalIgnoreCase),
                        HourlyRate = reader.GetDecimal("pay_rate"), IsActive = reader.GetBoolean("is_active")
                    });
                }
            }
        }

        // restore saved clock punches and the break supported by the current employee screen -H
        private static void LoadWorkPeriods(MySqlConnection connection)
        {
            TimeClockStore.WorkPeriods.Clear();
            const string sql = "SELECT id, employee_id, work_date, clock_in, break_start, break_end, clock_out FROM `440_timeclock_time_entries` ORDER BY work_date, id";
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    DateTime workDate = reader.GetDateTime("work_date").Date;
                    DateTime clockIn = ReadNullableDateTime(reader, "clock_in") ?? workDate;
                    DateTime? breakStart = ReadNullableDateTime(reader, "break_start");
                    DateTime? breakEnd = ReadNullableDateTime(reader, "break_end");
                    TimeClockStore.WorkPeriod period = new TimeClockStore.WorkPeriod
                    {
                        Id = reader.GetInt32("id"), EmployeeId = reader.GetInt32("employee_id"), ClockIn = clockIn,
                        BreakStart = breakStart, BreakEnd = breakEnd, ClockOut = ReadNullableDateTime(reader, "clock_out")
                    };
                    if (breakStart.HasValue) period.Breaks.Add(new TimeClockStore.WorkPeriod.BreakPeriod { Start = breakStart.Value, End = breakEnd });
                    TimeClockStore.WorkPeriods.Add(period);
                }
            }
        }

        // restore schedule rows as full dates and times used by the UI and punch checks -H
        private static void LoadShifts(MySqlConnection connection)
        {
            TimeClockStore.Shifts.Clear();
            const string sql = "SELECT id, employee_id, shift_date, start_time, end_time FROM `440_timeclock_schedules` ORDER BY shift_date, start_time";
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    DateTime day = reader.GetDateTime("shift_date").Date;
                    TimeClockStore.Shifts.Add(new TimeClockStore.Shift
                    {
                        Id = reader.GetInt32("id"), EmployeeId = reader.GetInt32("employee_id"),
                        Start = day.Add(reader.GetTimeSpan("start_time")), End = day.Add(reader.GetTimeSpan("end_time"))
                    });
                }
            }
        }
        // restore daily PTO requests and translate the stored rejected status to the UI wording -H
        private static void LoadPtoRequests(MySqlConnection connection)
        {
            TimeClockStore.TimeOffRequests.Clear();
            const string sql = "SELECT id, employee_id, requested_date, hours_requested, reason, status, reviewed_by, reviewed_at, created_at FROM `440_timeclock_pto_requests` ORDER BY requested_date, id";
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string storedStatus = reader.GetString("status");
                    string displayStatus = storedStatus.Equals("rejected", StringComparison.OrdinalIgnoreCase) || storedStatus.Equals("denied", StringComparison.OrdinalIgnoreCase)
                        ? "Denied" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(storedStatus);
                    TimeClockStore.TimeOffRequests.Add(new TimeClockStore.TimeOffRequest
                    {
                        Id = reader.GetInt32("id"), EmployeeId = reader.GetInt32("employee_id"),
                        Day = reader.GetDateTime("requested_date").Date, Hours = Convert.ToInt32(reader.GetDecimal("hours_requested")),
                        Reason = reader.GetString("reason"), Status = displayStatus, ReviewedBy = ReadNullableInt(reader, "reviewed_by"),
                        ReviewedAt = ReadNullableDateTime(reader, "reviewed_at"), Submitted = reader.GetDateTime("created_at")
                    });
                }
            }
        }

        // restore one weekly availability time window per employee and weekday -H
        private static void LoadAvailability(MySqlConnection connection)
        {
            TimeClockStore.Availabilities.Clear();
            const string sql = "SELECT employee_id, day_of_week, start_time, end_time FROM `440_timeclock_employee_availability` ORDER BY employee_id, id";
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                    TimeClockStore.Availabilities.Add(new TimeClockStore.Availability
                    {
                        EmployeeId = reader.GetInt32("employee_id"),
                        Day = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), reader.GetString("day_of_week"), true),
                        Start = reader.GetTimeSpan("start_time"), End = reader.GetTimeSpan("end_time")
                    });
            }
        }

        // load active points and leave reversed no-shows stored for history -H
        private static void LoadAttendancePoints(MySqlConnection connection)
        {
            TimeClockStore.AttendancePoints.Clear();
            const string sql = "SELECT id, employee_id, shift_start, points, reason, recorded_at, is_reversed FROM `440_timeclock_attendance_points` WHERE is_reversed = 0 ORDER BY recorded_at";
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                    TimeClockStore.AttendancePoints.Add(new TimeClockStore.AttendancePoint
                    {
                        Id = reader.GetInt64("id"), EmployeeId = reader.GetInt32("employee_id"),
                        ShiftStart = reader.GetDateTime("shift_start"), Points = reader.GetDecimal("points"),
                        Reason = reader.GetString("reason"), RecordedAt = reader.GetDateTime("recorded_at"), IsReversed = false
                    });
            }
        }

        // restore audit rows for future review and reporting -H
        private static void LoadAudit(MySqlConnection connection)
        {
            TimeClockStore.Audit.Clear();
            const string sql = "SELECT manager_id, record_id, old_values, new_values, created_at FROM `440_timeclock_audit_log` ORDER BY id";
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                    TimeClockStore.Audit.Add(new TimeClockStore.AuditEntry
                    {
                        ManagerId = reader.GetInt32("manager_id"), RecordId = reader.GetInt64("record_id"),
                        OldValues = reader.IsDBNull(reader.GetOrdinal("old_values")) ? null : reader.GetString("old_values"),
                        NewValues = reader.IsDBNull(reader.GetOrdinal("new_values")) ? null : reader.GetString("new_values"),
                        ChangedAt = reader.GetDateTime("created_at")
                    });
            }
        }

        // use saved weekday preferences when present and keep demo defaults for an empty table -H
        private static void LoadSchedulePreferences(MySqlConnection connection)
        {
            List<TimeClockStore.ScheduleDayPreference> saved = new List<TimeClockStore.ScheduleDayPreference>();
            const string sql = "SELECT day_of_week, is_open, opening_time, closing_time, minimum_staff FROM `440_timeclock_schedule_preferences` ORDER BY day_of_week";
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                    saved.Add(new TimeClockStore.ScheduleDayPreference
                    {
                        Day = (DayOfWeek)reader.GetByte("day_of_week"), IsOpen = reader.GetBoolean("is_open"),
                        OpeningTime = reader.IsDBNull(reader.GetOrdinal("opening_time")) ? TimeSpan.Zero : reader.GetTimeSpan("opening_time"),
                        ClosingTime = reader.IsDBNull(reader.GetOrdinal("closing_time")) ? TimeSpan.Zero : reader.GetTimeSpan("closing_time"),
                        MinimumStaff = reader.IsDBNull(reader.GetOrdinal("minimum_staff")) ? 1 : reader.GetInt16("minimum_staff")
                    });
            }
            if (saved.Count > 0)
            {
                TimeClockStore.SchedulePreferences.Clear();
                TimeClockStore.SchedulePreferences.AddRange(saved);
            }
        }

        // restore saved week-by-week opening, closing, and staffing overrides -H
        private static void LoadScheduleWeekSettings(MySqlConnection connection)
        {
            TimeClockStore.ScheduleWeekEdits.Clear();
            const string sql = "SELECT week_start, day_of_week, is_open, opening_time, closing_time, minimum_staff FROM `440_timeclock_schedule_week_settings` ORDER BY week_start, day_of_week";
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    DateTime week = reader.GetDateTime("week_start").Date;
                    int day = reader.GetByte("day_of_week");
                    string[] values;
                    if (!TimeClockStore.ScheduleWeekEdits.TryGetValue(week, out values))
                    {
                        values = new string[21];
                        TimeClockStore.ScheduleWeekEdits.Add(week, values);
                    }
                    if (reader.GetBoolean("is_open"))
                    {
                        values[day * 3] = FormatTime(reader.GetTimeSpan("opening_time"));
                        values[day * 3 + 1] = FormatTime(reader.GetTimeSpan("closing_time"));
                        values[day * 3 + 2] = reader.GetInt16("minimum_staff").ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        // restore saved setup and cleanup buffers from the singleton settings row -H
        private static void LoadScheduleGenerationSettings(MySqlConnection connection)
        {
            const string sql = "SELECT setup_minutes, cleanup_minutes FROM `440_timeclock_schedule_generation_settings` WHERE settings_id = 1";
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            using (MySqlDataReader reader = command.ExecuteReader())
                if (reader.Read())
                {
                    TimeClockStore.SetupMinutes = reader.GetInt16("setup_minutes");
                    TimeClockStore.CleanupMinutes = reader.GetInt16("cleanup_minutes");
                }
        }
        // write one timesheet row and retain its ID for manager audit records -H
        internal static void SaveWorkPeriod(TimeClockStore.WorkPeriod period, int? editedBy = null)
        {
            using (MySqlConnection connection = OpenConnection())
            using (MySqlTransaction transaction = connection.BeginTransaction())
            {
                const string sql = "INSERT INTO `440_timeclock_time_entries` (employee_id, work_date, clock_in, break_start, break_end, clock_out) VALUES (@employee, @date, @clockIn, @breakStart, @breakEnd, @clockOut) ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id), clock_in = VALUES(clock_in), break_start = VALUES(break_start), break_end = VALUES(break_end), clock_out = VALUES(clock_out)";
                using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@employee", period.EmployeeId);
                    command.Parameters.AddWithValue("@date", period.ClockIn.Date);
                    command.Parameters.AddWithValue("@clockIn", period.ClockIn);
                    command.Parameters.AddWithValue("@breakStart", (object)period.BreakStart ?? DBNull.Value);
                    command.Parameters.AddWithValue("@breakEnd", (object)period.BreakEnd ?? DBNull.Value);
                    command.Parameters.AddWithValue("@clockOut", (object)period.ClockOut ?? DBNull.Value);
                    command.ExecuteNonQuery();
                    period.Id = checked((int)command.LastInsertedId);
                }
                if (editedBy.HasValue)
                {
                    const string auditSql = "UPDATE `440_timeclock_time_entries` SET edited_by = @editor, edited_at = @editedAt WHERE id = @id";
                    using (MySqlCommand command = new MySqlCommand(auditSql, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@editor", editedBy.Value);
                        command.Parameters.AddWithValue("@editedAt", TimeClockStore.Now);
                        command.Parameters.AddWithValue("@id", period.Id);
                        command.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }

        // save every date in a PTO range in one transaction to prevent partial requests -H
        internal static void SavePtoRequests(IEnumerable<TimeClockStore.TimeOffRequest> requests)
        {
            using (MySqlConnection connection = OpenConnection())
            using (MySqlTransaction transaction = connection.BeginTransaction())
            {
                const string sql = "INSERT INTO `440_timeclock_pto_requests` (employee_id, requested_date, hours_requested, reason, status, created_at) VALUES (@employee, @date, @hours, @reason, 'pending', @submitted)";
                foreach (TimeClockStore.TimeOffRequest request in requests)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@employee", request.EmployeeId);
                        command.Parameters.AddWithValue("@date", request.Day.Date);
                        command.Parameters.AddWithValue("@hours", request.Hours);
                        command.Parameters.AddWithValue("@reason", request.Reason);
                        command.Parameters.AddWithValue("@submitted", request.Submitted);
                        command.ExecuteNonQuery();
                        request.Id = checked((int)command.LastInsertedId);
                    }
                }
                transaction.Commit();
            }
        }

        // save a manager's approval or rejection for the selected PTO date -H
        internal static void SavePtoDecision(TimeClockStore.TimeOffRequest request)
        {
            string status = request.Status == "Approved" ? "approved" : request.Status == "Denied" ? "rejected" : "pending";
            const string sql = "UPDATE `440_timeclock_pto_requests` SET status = @status, reviewed_by = @reviewer, reviewed_at = @reviewedAt WHERE id = @id";
            using (MySqlConnection connection = OpenConnection())
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@status", status);
                command.Parameters.AddWithValue("@reviewer", (object)request.ReviewedBy ?? DBNull.Value);
                command.Parameters.AddWithValue("@reviewedAt", (object)request.ReviewedAt ?? DBNull.Value);
                command.Parameters.AddWithValue("@id", request.Id);
                command.ExecuteNonQuery();
            }
        }

        // replace one employee's availability windows in a single transaction -H
        internal static void ReplaceAvailability(int employeeId, IEnumerable<TimeClockStore.Availability> entries)
        {
            using (MySqlConnection connection = OpenConnection())
            using (MySqlTransaction transaction = connection.BeginTransaction())
            {
                using (MySqlCommand delete = new MySqlCommand("DELETE FROM `440_timeclock_employee_availability` WHERE employee_id = @employee", connection, transaction))
                {
                    delete.Parameters.AddWithValue("@employee", employeeId);
                    delete.ExecuteNonQuery();
                }
                const string sql = "INSERT INTO `440_timeclock_employee_availability` (employee_id, day_of_week, start_time, end_time) VALUES (@employee, @day, @start, @end)";
                foreach (TimeClockStore.Availability entry in entries)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@employee", entry.EmployeeId);
                        command.Parameters.AddWithValue("@day", entry.Day.ToString());
                        command.Parameters.AddWithValue("@start", entry.Start);
                        command.Parameters.AddWithValue("@end", entry.End);
                        command.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }

        // insert one unique attendance event for the employee's scheduled shift -H
        internal static void SaveAttendancePoint(TimeClockStore.AttendancePoint point)
        {
            const string sql = "INSERT INTO `440_timeclock_attendance_points` (employee_id, shift_start, points, reason, recorded_at, is_reversed) VALUES (@employee, @shiftStart, @points, @reason, @recordedAt, 0) ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id), points = VALUES(points), is_reversed = 0, reversed_at = NULL";
            using (MySqlConnection connection = OpenConnection())
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@employee", point.EmployeeId);
                command.Parameters.AddWithValue("@shiftStart", point.ShiftStart);
                command.Parameters.AddWithValue("@points", point.Points);
                command.Parameters.AddWithValue("@reason", point.Reason);
                command.Parameters.AddWithValue("@recordedAt", point.RecordedAt);
                command.ExecuteNonQuery();
                point.Id = command.LastInsertedId;
            }
        }

        // mark a corrected no-show as reversed without deleting its history -H
        internal static void ReverseNoShow(int employeeId, DateTime shiftStart, DateTime reversedAt)
        {
            const string sql = "UPDATE `440_timeclock_attendance_points` SET is_reversed = 1, reversed_at = @reversedAt WHERE employee_id = @employee AND shift_start = @shiftStart AND reason = 'No show'";
            using (MySqlConnection connection = OpenConnection())
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@reversedAt", reversedAt);
                command.Parameters.AddWithValue("@employee", employeeId);
                command.Parameters.AddWithValue("@shiftStart", shiftStart);
                command.ExecuteNonQuery();
            }
        }
        // reverse a corrected attendance reason without deleting its audit history -H
        internal static void ReverseAttendancePoint(int employeeId, DateTime shiftStart, string reason, DateTime reversedAt)
        {
            const string sql = "UPDATE `440_timeclock_attendance_points` SET is_reversed = 1, reversed_at = @reversedAt WHERE employee_id = @employee AND shift_start = @shiftStart AND reason = @reason AND is_reversed = 0";
            using (MySqlConnection connection = OpenConnection())
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@reversedAt", reversedAt);
                command.Parameters.AddWithValue("@employee", employeeId);
                command.Parameters.AddWithValue("@shiftStart", shiftStart);
                command.Parameters.AddWithValue("@reason", reason);
                command.ExecuteNonQuery();
            }
        }
        // replace saved shifts for selected dates and commit the complete generated schedule -H
        internal static void ReplaceScheduleDates(IList<DateTime> dates, IList<TimeClockStore.Shift> shifts)
        {
            if (dates == null || dates.Count == 0) return;
            using (MySqlConnection connection = OpenConnection())
            using (MySqlTransaction transaction = connection.BeginTransaction())
            {
                List<string> names = dates.Select((date, index) => "@date" + index.ToString(CultureInfo.InvariantCulture)).ToList();
                using (MySqlCommand delete = new MySqlCommand("DELETE FROM `440_timeclock_schedules` WHERE shift_date IN (" + String.Join(",", names) + ")", connection, transaction))
                {
                    for (int index = 0; index < dates.Count; index++) delete.Parameters.AddWithValue(names[index], dates[index].Date);
                    delete.ExecuteNonQuery();
                }
                const string sql = "INSERT INTO `440_timeclock_schedules` (employee_id, shift_date, start_time, end_time) VALUES (@employee, @date, @start, @end)";
                foreach (TimeClockStore.Shift shift in shifts)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
                    {
                        AddShiftParameters(command, shift);
                        command.ExecuteNonQuery();
                        shift.Id = checked((int)command.LastInsertedId);
                    }
                }
                transaction.Commit();
            }
        }

        // save a manually created or edited shift without replacing other schedule rows -H
        internal static void SaveShift(TimeClockStore.Shift shift)
        {
            string sql = shift.Id > 0
                ? "UPDATE `440_timeclock_schedules` SET employee_id = @employee, shift_date = @date, start_time = @start, end_time = @end WHERE id = @id"
                : "INSERT INTO `440_timeclock_schedules` (employee_id, shift_date, start_time, end_time) VALUES (@employee, @date, @start, @end)";
            using (MySqlConnection connection = OpenConnection())
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                AddShiftParameters(command, shift);
                if (shift.Id > 0) command.Parameters.AddWithValue("@id", shift.Id);
                command.ExecuteNonQuery();
                if (shift.Id == 0) shift.Id = checked((int)command.LastInsertedId);
            }
        }

        // delete a manager-selected shift by its database ID -H
        internal static void DeleteShift(TimeClockStore.Shift shift)
        {
            if (shift.Id == 0) return;
            using (MySqlConnection connection = OpenConnection())
            using (MySqlCommand command = new MySqlCommand("DELETE FROM `440_timeclock_schedules` WHERE id = @id", connection))
            {
                command.Parameters.AddWithValue("@id", shift.Id);
                command.ExecuteNonQuery();
            }
        }

        // save reusable weekday hours and prep or cleanup buffers as one change -H
        internal static void SaveSchedulePreferences(IList<TimeClockStore.ScheduleDayPreference> preferences, int setupMinutes, int cleanupMinutes, int managerId)
        {
            using (MySqlConnection connection = OpenConnection())
            using (MySqlTransaction transaction = connection.BeginTransaction())
            {
                using (MySqlCommand delete = new MySqlCommand("DELETE FROM `440_timeclock_schedule_preferences`", connection, transaction)) delete.ExecuteNonQuery();
                const string sql = "INSERT INTO `440_timeclock_schedule_preferences` (day_of_week, is_open, opening_time, closing_time, minimum_staff, updated_by) VALUES (@day, @open, @opening, @closing, @staff, @manager)";
                foreach (TimeClockStore.ScheduleDayPreference preference in preferences)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@day", (int)preference.Day);
                        command.Parameters.AddWithValue("@open", preference.IsOpen);
                        command.Parameters.AddWithValue("@opening", preference.IsOpen ? (object)preference.OpeningTime : DBNull.Value);
                        command.Parameters.AddWithValue("@closing", preference.IsOpen ? (object)preference.ClosingTime : DBNull.Value);
                        command.Parameters.AddWithValue("@staff", preference.IsOpen ? (object)preference.MinimumStaff : DBNull.Value);
                        command.Parameters.AddWithValue("@manager", managerId);
                        command.ExecuteNonQuery();
                    }
                }
                const string settingsSql = "INSERT INTO `440_timeclock_schedule_generation_settings` (settings_id, setup_minutes, cleanup_minutes, updated_by) VALUES (1, @setup, @cleanup, @manager) ON DUPLICATE KEY UPDATE setup_minutes = VALUES(setup_minutes), cleanup_minutes = VALUES(cleanup_minutes), updated_by = VALUES(updated_by)";
                using (MySqlCommand settings = new MySqlCommand(settingsSql, connection, transaction))
                {
                    settings.Parameters.AddWithValue("@setup", setupMinutes);
                    settings.Parameters.AddWithValue("@cleanup", cleanupMinutes);
                    settings.Parameters.AddWithValue("@manager", managerId);
                    settings.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }

        // save daily overrides only when each day contains valid HHMM values or is blank -H
        internal static bool SaveScheduleWeek(DateTime weekStart, string[] values, int managerId)
        {
            if (values == null || values.Length != 21) return false;
            TimeSpan[] starts = new TimeSpan[7];
            TimeSpan[] ends = new TimeSpan[7];
            int[] staff = new int[7];
            bool[] isOpen = new bool[7];
            for (int day = 0; day < 7; day++)
            {
                string opening = values[day * 3];
                string closing = values[day * 3 + 1];
                string count = values[day * 3 + 2];
                if (String.IsNullOrWhiteSpace(opening) && String.IsNullOrWhiteSpace(closing) && String.IsNullOrWhiteSpace(count)) continue;
                if (!TryParseHhmm(opening, out starts[day]) || !TryParseHhmm(closing, out ends[day]) || ends[day] <= starts[day] || !Int32.TryParse(count, out staff[day]) || staff[day] < 1) return false;
                isOpen[day] = true;
            }
            using (MySqlConnection connection = OpenConnection())
            using (MySqlTransaction transaction = connection.BeginTransaction())
            {
                using (MySqlCommand delete = new MySqlCommand("DELETE FROM `440_timeclock_schedule_week_settings` WHERE week_start = @week", connection, transaction))
                {
                    delete.Parameters.AddWithValue("@week", weekStart.Date);
                    delete.ExecuteNonQuery();
                }
                const string sql = "INSERT INTO `440_timeclock_schedule_week_settings` (week_start, day_of_week, is_open, opening_time, closing_time, minimum_staff, updated_by) VALUES (@week, @day, @open, @start, @end, @staff, @manager)";
                for (int day = 0; day < 7; day++)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@week", weekStart.Date);
                        command.Parameters.AddWithValue("@day", day);
                        command.Parameters.AddWithValue("@open", isOpen[day]);
                        command.Parameters.AddWithValue("@start", isOpen[day] ? (object)starts[day] : DBNull.Value);
                        command.Parameters.AddWithValue("@end", isOpen[day] ? (object)ends[day] : DBNull.Value);
                        command.Parameters.AddWithValue("@staff", isOpen[day] ? (object)staff[day] : DBNull.Value);
                        command.Parameters.AddWithValue("@manager", managerId);
                        command.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
            return true;
        }
        // write the manager's before-and-after timesheet data as JSON for the audit table -H
        internal static void SaveAudit(TimeClockStore.AuditEntry audit)
        {
            string oldJson = AuditJson(audit.EmployeeId, audit.Reason, audit.OldValues ?? "");
            string newJson = AuditJson(audit.EmployeeId, audit.Reason, audit.NewValues ?? audit.Change ?? "");
            const string sql = "INSERT INTO `440_timeclock_audit_log` (manager_id, action_type, table_name, record_id, old_values, new_values, created_at) VALUES (@manager, 'timesheet_edit', '440_timeclock_time_entries', @record, @old, @new, @created)";
            using (MySqlConnection connection = OpenConnection())
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@manager", audit.ManagerId);
                command.Parameters.AddWithValue("@record", audit.RecordId);
                command.Parameters.AddWithValue("@old", oldJson);
                command.Parameters.AddWithValue("@new", newJson);
                command.Parameters.AddWithValue("@created", audit.ChangedAt);
                command.ExecuteNonQuery();
            }
        }

        // share date and time parameters between schedule insert and update commands -H
        private static void AddShiftParameters(MySqlCommand command, TimeClockStore.Shift shift)
        {
            command.Parameters.AddWithValue("@employee", shift.EmployeeId);
            command.Parameters.AddWithValue("@date", shift.Start.Date);
            command.Parameters.AddWithValue("@start", shift.Start.TimeOfDay);
            command.Parameters.AddWithValue("@end", shift.End.TimeOfDay);
        }

        // convert a saved schedule time back to the editor's four-digit format -H
        private static string FormatTime(TimeSpan time)
        {
            return ((int)time.TotalHours).ToString("00", CultureInfo.InvariantCulture) + time.Minutes.ToString("00", CultureInfo.InvariantCulture);
        }

        // parse the manager's 24-hour HHMM input before storing its TIME value -H
        private static bool TryParseHhmm(string value, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            int numeric;
            if (!Int32.TryParse(value, out numeric) || numeric < 0 || numeric > 2359) return false;
            int hour = numeric / 100;
            int minute = numeric % 100;
            if (hour > 23 || minute > 59) return false;
            time = new TimeSpan(hour, minute, 0);
            return true;
        }

        // read nullable columns without converting SQL NULL to a default value -H
        private static DateTime? ReadNullableDateTime(MySqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? (DateTime?)null : reader.GetDateTime(ordinal);
        }

        // read nullable user IDs such as PTO reviewers -H
        private static int? ReadNullableInt(MySqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? (int?)null : reader.GetInt32(ordinal);
        }

        // build valid JSON from the employee ID, manager reason, and timesheet snapshot -H
        private static string AuditJson(int employeeId, string reason, string value)
        {
            return "{\"employee_id\":" + employeeId.ToString(CultureInfo.InvariantCulture) + ",\"reason\":\"" + EscapeJson(reason) + "\",\"timesheet\":\"" + EscapeJson(value) + "\"}";
        }

        // escape quotes and control characters before placing text inside JSON -H
        private static string EscapeJson(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
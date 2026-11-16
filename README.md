# Employee Time Clock

A Windows Forms proof-of-concept for employee timekeeping, schedules, availability, PTO requests, attendance points, and weekly pay estimates. The application uses MariaDB for saved user and timeclock data.

## Requirements and startup

- Windows and a recent version of Visual Studio that supports the `EmployeeTimeClock.slnx` solution
- The .NET Framework 4.7.2 developer/targeting pack
- NuGet access so Visual Studio can restore `MySqlConnector` 2.6.1
- Access to the Eastern Kentucky University MariaDB server from campus or through the EKU VPN
- The `csc340_db` schema with the timeclock tables listed below

Open `EmployeeTimeClock.slnx` from the repository root and run the `EmployeeTimeClock` project. Database connection settings are centralized in `EmployeeTimeClock/DatabaseConfig.cs`. The app loads its database records before displaying the login screen, so a database connection is required to start it.

The project expects these tables in `csc340_db`:

- `440_timeclock_users`
- `440_timeclock_time_entries`
- `440_timeclock_schedules`
- `440_timeclock_pto_requests`
- `440_timeclock_employee_availability`
- `440_timeclock_attendance_points`
- `440_timeclock_audit_log`
- `440_timeclock_schedule_preferences`
- `440_timeclock_schedule_week_settings`
- `440_timeclock_schedule_generation_settings`

The application does not create or migrate these tables automatically.

## Demo accounts

The seeded demo accounts share a password by role:

- **Manager accounts:** `admin123`
- **Employee accounts:** `employee123`

Passwords are stored in MariaDB as SHA-256 hashes. The app has no password reset or account creation screen. Usernames below use the requested rule of first initial plus the first four letters of the last name. If the rename SQL was not run on a particular database copy, the database may still have its previous usernames. To confirm the values saved in your copy, run:

```sql
SELECT display_name, username, role
FROM `440_timeclock_users`
WHERE is_active = 1
ORDER BY role, display_name;
```

| Role | Display name | Username | Password |
| Manager | Morgan Manager | `mmana` | `admin123` |
| Manager | Sam Thompson | `sthom` | `admin123` |
| Manager | Drew Robinson | `drobi` | `admin123` |
| Employee | John Smith | `jsmit` | `employee123` |
| Employee | Alex Johnson | `ajohn` | `employee123` |
| Employee | Taylor Smith | `tsmit` | `employee123` |
| Employee | Jordan Williams | `jwill` | `employee123` |
| Employee | Casey Brown | `cbrow` | `employee123` |
| Employee | Morgan Davis | `mdavi` | `employee123` |
| Employee | Riley Wilson | `rwils` | `employee123` |
| Employee | Jamie Miller | `jmill` | `employee123` |
| Employee | Avery Moore | `amoor` | `employee123` |
| Employee | Cameron Taylor | `ctayl` | `employee123` |
| Employee | Quinn Anderson | `qande` | `employee123` |

## Employee features

- Clock in and out against the employee's saved schedule
- Start and end a recorded lunch break
- View a monthly schedule calendar with assigned shift times
- View a selected day's clock-in, lunch, clock-out, and worked-hour totals
- Submit weekly availability by weekday; an empty day means unavailable
- Submit PTO for one date or an inclusive date range, with requested hours and a reason
- View attendance points in the employee screen

Clock-in is blocked more than 15 minutes before the scheduled start and after the scheduled shift has ended. A clock-in more than 15 minutes late warns the employee and adds 0.5 points if confirmed. Lunch cannot start until three hours after clock-in, and it cannot end until at least 30 minutes have elapsed. Clocking out more than 15 minutes early or more than 15 minutes after the scheduled end warns the employee and adds 0.5 points if confirmed.

## Manager features

- View any employee's or manager's monthly schedule calendar
- View and edit employee timesheets, including creating an entry for a selected date
- Review an employee's submitted weekly availability
- Review PTO requests and approve or deny pending requests
- Edit, assign, or remove an individual shift
- Set recurring weekday business hours, setup/cleanup buffers, and minimum employee coverage
- Set week-specific business-hour and staffing overrides
- Generate schedules for the selected week
- Calculate a weekly gross-pay estimate from recorded hours and the employee's hourly rate
- View attendance points while reviewing an employee schedule

Manager timesheet edits require a reason and are written to the audit log with the old and new values.

## Schedule generation

1. The manager selects a Sunday-start schedule week. Past weeks are unavailable for generation; the current and future weeks are offered.
2. Each day can use the saved recurring business-hour preference or a week-specific override. A blank day is treated as closed.
3. The manager sets opening time, closing time, and the minimum number of **employees** needed in each generated coverage block. Managers do not count toward this employee minimum.
4. Setup time is subtracted from opening time and cleanup time is added after closing time to form the staffing coverage window. The generator divides that window into consecutive shift blocks, each no longer than eight hours.
5. For each employee block, the generator selects an active employee whose availability covers the whole block, who has no other shift that date, and who does not have a pending or approved PTO request for that date. It prefers employees with fewer hours already assigned in the generated week.
6. One manager is assigned separately to each open day. The manager shift is centered between store opening and closing, is no longer than eight hours, and is not counted toward employee coverage. Manager availability records are ignored; a non-denied PTO request still prevents assignment.
7. The schedule is saved only after all required coverage can be staffed. If employee or manager coverage is missing, the application shows the gaps and keeps the existing schedule unchanged.

For a store open from 9 AM to 9 PM, an eight-hour centered manager shift is 11 AM to 7 PM. The default example preferences are 9 AM to 9 PM, one employee per coverage block, and 15 minutes of setup and cleanup; managers can change these in the schedule preference screen.

The generator skips past dates. It also preserves today's schedule if any shift or time entry already exists for today. Manually edited shifts are limited to eight hours and checked against business hours and the selected worker's saved availability.

## Attendance and pay calculations

- Attendance policy: 0.5 points for clocking in over 15 minutes late, leaving over 15 minutes early, or working over 15 minutes past the scheduled end; 1 point for a no-show
- No-show points are inferred when a scheduled shift has ended and its grace period passed without a clock-in, unless the employee has approved PTO. The check runs when a manager opens the screen. If the employee later clocks in, the inferred no-show is reversed.
- The payment screen estimates gross pay as recorded worked hours multiplied by the stored hourly rate. It does not issue payments or calculate taxes, deductions, or overtime.
- The pay screen defaults to the previous complete Sunday-through-Saturday period and lets the manager choose any completed week from the past six months.
- NTP time sync is attempted when a user signs in and refreshed periodically. If unavailable, the app uses the computer's local clock. Sync status is written to Visual Studio's Debug Output, not shown in the GUI.

## Project notes

- This is a school proof-of-concept, not a production payroll or HR system.
- Demo passwords are shared by role and should only be used with the class database.
- Changes to accounts, schedules, timesheets, PTO, attendance, and manager edits are saved to MariaDB.
- Build artifacts and package restore files are generated by Visual Studio and are not required in source control.
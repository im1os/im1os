# Human Resources Scope

Human Resources is a core iM1 OS module, not a secondary administration feature. Every person working for a company is modeled as an `Employee`. Some employees have login accounts. Some do not.

This distinction is foundational:

```text
Employee
  -> Has login account? Yes/No
    -> Company role
    -> Permissions
    -> MFA
    -> Password
```

A mechanic may clock in every day from a kiosk or badge and never log in to the management system. A seasonal employee may only use the time clock. A contractor may need OSHA documents without a login. An accountant may not clock in but may have full access to Accounting.

Designing iM1 OS around Employee first prevents a later migration from a "Users" concept to an "Employees" concept. Employee is one of the master records of the system, alongside Customer, Vehicle, Vendor, Product, and Company.

## Module Placement

Human Resources lives inside tenant Business Administration and becomes its own core operating module as the product grows.

```text
Administration

Human Resources
  Employees
  Time Clock
  Schedules
  Time Off
  Payroll
  Commissions
  Assets
  Documents
  OSHA
```

## Employee Record

### General

- Employee number
- First name
- Last name
- Preferred name
- Email
- Phone
- Address
- Emergency contact
- Department
- Manager
- Job title
- Status
- Hire date
- Termination date
- Rehire eligible

### Employment

- Employment type: full time, part time, seasonal, contractor
- Pay type: salary, hourly, commission, mixed
- Current pay rate
- Effective date history

Pay rate history must never be overwritten. Changes are appended with effective dates.

```text
$18.00  2026-01-01
$20.00  2026-05-01
$22.00  2026-10-01
```

### Login

Only present when login access is enabled for the employee.

- Username
- Password credential
- MFA
- Company role
- Permissions
- Active sessions

### Time Clock

- Clock in
- Clock out
- Missed punches
- Overtime
- Breaks
- GPS, optional
- Photo verification, future

### Work Schedule

Employees support weekly recurring schedules, multiple schedules, and holiday exceptions.

```text
Monday
  8:00 - 5:00

Tuesday
  9:00 - 6:00
```

### Time Off

- PTO
- Vacation
- Sick
- Jury duty
- Bereavement
- Unpaid
- Approval workflow
- Calendar integration

### Payroll

Payroll integration should support QuickBooks Payroll first.

- Payroll ID
- Employee ID
- Last sync
- Payroll status

### Commissions

Commissions are a differentiating iM1 OS capability and should be versioned by effective date.

Sales commissions:

- Effective date
- Commission plan
- Percentage
- Overrides

Work order commissions:

- Labor percentage
- Parts percentage
- Flat rate
- Technician bonus
- Effective dates

### Certifications

- OSHA
- Forklift
- First Aid
- CPR
- Manufacturer certifications
- ASE
- Expiration dates
- Automatic reminders

### Documents

Employees need a document vault.

- Driver license
- I-9
- W-4
- W-2
- Signed handbook
- NDA
- Drug test
- Performance reviews
- Training certificates
- Custom documents

Each document supports:

- Expiration
- Effective date
- Version
- Digital signature
- Notes

### Assets

Track issued assets.

- Laptop
- Desktop
- Tablet
- Phone
- Keys
- Key fobs
- Uniforms
- Tool box
- Company credit card
- Vehicle
- Scanner
- Barcode gun
- Printer
- POS terminal

Each asset assignment tracks:

- Serial number
- Assigned date
- Returned date
- Condition
- Photos

### OSHA And Safety

- Incidents
- Injuries
- Near misses
- OSHA 300 log
- Workers comp
- Safety training

### Performance

Future scope:

- Reviews
- Goals
- Write-ups
- Coaching
- Recognition

### Activity Timeline

Employee history must be captured as immutable timeline and audit events.

```text
Employee created
Clocked in
Clocked out
Pay rate changed
Role changed
Password reset
Document uploaded
Asset assigned
OSHA report filed
```

## Connected Modules

Employee connects to nearly every major area of iM1 OS:

```text
Employee
  -> Authentication
  -> Security
  -> CRM
  -> Work Orders
  -> Sales
  -> Estimates
  -> Scheduling
  -> Time Clock
  -> Payroll
  -> Accounting
  -> Inventory
  -> Purchasing
  -> Asset Management
  -> Document Management
  -> HR
  -> OSHA
  -> Commissions
  -> Reporting
  -> Audit Logs
```

## Implementation Rules

- Do not use "User" as the business-facing model for people who work for the company.
- `Employee` is the company worker record whether or not login access exists.
- Login accounts, roles, permissions, MFA, password credentials, and sessions are optional access concerns attached to an employee.
- Employee records are tenant-owned and must contain `OrganizationId`.
- Location-specific employment, schedule, clock, asset, and operational assignment records should contain `LocationId` when the workflow occurs at a specific location.
- Employee personal, payroll, document, health, and safety data is sensitive and must be permissioned, audited, and excluded from tenant-safe aggregation unless explicitly designed otherwise.

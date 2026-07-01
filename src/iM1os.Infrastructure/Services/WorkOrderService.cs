using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.WorkOrders;
using iM1os.Domain.Audit;
using iM1os.Domain.Customers;
using iM1os.Domain.Service;
using iM1os.Domain.Tenancy;
using iM1os.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class WorkOrderService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : IWorkOrderService
{
    public async Task<WorkOrderWorkspace> GetWorkspaceAsync(Guid organizationId, WorkOrderSearchRequest request, CancellationToken cancellationToken)
    {
        var workOrders = await dbContext.WorkOrders.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Stage))
        {
            workOrders = workOrders
                .Where(x => string.Equals(x.Stage, request.Stage.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var customerIds = workOrders.Select(x => x.CustomerId).Distinct().ToArray();
        var vehicleIds = workOrders.Select(x => x.CustomerVehicleId).OfType<Guid>().Distinct().ToArray();
        var advisorIds = workOrders.Select(x => x.ServiceAdvisorEmployeeId).OfType<Guid>().Distinct().ToArray();
        var workOrderIds = workOrders.Select(x => x.Id).ToArray();

        var customers = await dbContext.Customers.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && customerIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var vehicles = await dbContext.CustomerVehicles.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && vehicleIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Year, x.Make, x.Model, x.Vin })
            .ToDictionaryAsync(x => x.Id, x => UnitLabel(x.Year, x.Make, x.Model, x.Vin), cancellationToken);

        var advisors = await dbContext.Employees.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && advisorIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var estimateTotals = await dbContext.Estimates.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && workOrderIds.Contains(x.WorkOrderId))
            .GroupBy(x => x.WorkOrderId)
            .Select(x => new
            {
                WorkOrderId = x.Key,
                Total = x.OrderByDescending(estimate => estimate.ApprovedAtUtc ?? estimate.CreatedForCustomerAtUtc)
                    .Select(estimate => estimate.GrandTotal)
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.WorkOrderId, x => x.Total, cancellationToken);

        var rows = workOrders
            .Select(x => new WorkOrderRow(
                x.Id,
                x.WorkOrderNumber,
                x.RepairOrderNumber,
                customers.GetValueOrDefault(x.CustomerId, "Unknown Customer"),
                x.CustomerVehicleId is Guid vehicleId ? vehicles.GetValueOrDefault(vehicleId) : null,
                x.Stage,
                x.Priority,
                x.ServiceAdvisorEmployeeId is Guid advisorId ? advisors.GetValueOrDefault(advisorId) : null,
                x.PromiseDate,
                x.OpenedAtUtc,
                estimateTotals.GetValueOrDefault(x.Id)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var search = request.Query.Trim();
            rows = rows
                .Where(x =>
                    Contains(x.WorkOrderNumber, search) ||
                    Contains(x.RepairOrderNumber, search) ||
                    Contains(x.CustomerName, search) ||
                    Contains(x.Unit, search) ||
                    Contains(x.ServiceAdvisor, search) ||
                    Contains(x.Stage, search) ||
                    Contains(x.Priority, search))
                .ToList();
        }

        return new WorkOrderWorkspace(rows, request.Query, request.Stage);
    }

    public async Task<WorkOrderEditor> GetNewEditorAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var customers = await GetCustomersAsync(organizationId, cancellationToken);
        var vehicles = await GetVehiclesAsync(organizationId, cancellationToken);
        var configuration = await GetBusinessConfigurationAsync(organizationId, cancellationToken);
        return new WorkOrderEditor(
            null,
            await GenerateWorkOrderNumberAsync(organizationId, cancellationToken),
            null,
            null,
            null,
            null,
            "Intake / Check-in",
            "Normal",
            null,
            DateOnly.FromDateTime(dateTimeProvider.UtcNow.DateTime),
            null,
            null,
            null,
            null,
            "No Deposit",
            "Payment due on completion.",
            configuration.DefaultLaborRate,
            configuration.LaborLineItemsTaxable,
            null,
            [],
            [BlankLineItem(configuration.LaborLineItemsTaxable)],
            new WorkOrderTotals(0m, 0m, 0m, 0m, 0m, 0m, 0m),
            customers,
            vehicles,
            null,
            null,
            await GetServiceAdvisorsAsync(organizationId, cancellationToken),
            await GetTechniciansAsync(organizationId, cancellationToken));
    }

    public async Task<WorkOrderEditor?> GetEditorAsync(Guid organizationId, Guid workOrderId, CancellationToken cancellationToken)
    {
        var workOrder = await dbContext.WorkOrders.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.Id == workOrderId)
            .Include(x => x.TechnicianAssignments)
            .Include(x => x.Estimates)
            .SingleOrDefaultAsync(cancellationToken);
        if (workOrder is null)
        {
            return null;
        }

        var estimate = await dbContext.Estimates.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.WorkOrderId == workOrder.Id)
            .OrderByDescending(x => x.ApprovedAtUtc ?? x.CreatedForCustomerAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var lineItems = estimate is null
            ? []
            : await dbContext.EstimateLineItems.IgnoreQueryFilters()
                .Where(x => x.OrganizationId == organizationId && x.EstimateId == estimate.Id)
                .OrderBy(x => x.SortOrder)
                .Select(x => new EstimateLineItemEditor(
                    x.Id,
                    x.LineType,
                    x.Description,
                    x.Notes,
                    x.Sku,
                    x.Quantity,
                    x.Rate,
                    x.DiscountAmount,
                    x.DiscountPercent,
                    x.LineTotal,
                    x.IsTaxable,
                    x.IsDeclined,
                    x.IsDone,
                    x.SortOrder))
                .ToListAsync(cancellationToken);

        if (lineItems.Count == 0)
        {
            var blankConfiguration = await GetBusinessConfigurationAsync(organizationId, cancellationToken);
            lineItems.Add(BlankLineItem(blankConfiguration.LaborLineItemsTaxable));
        }

        var assignments = workOrder.TechnicianAssignments.OrderBy(x => x.SortOrder).ToList();
        var lead = assignments.FirstOrDefault(x => string.Equals(x.Role, "Lead Technician", StringComparison.OrdinalIgnoreCase));
        var additional = assignments
            .Where(x => !string.Equals(x.Role, "Lead Technician", StringComparison.OrdinalIgnoreCase))
            .Select(x => new WorkOrderTechnicianAssignmentItem(x.EmployeeId, x.Role, x.SplitPercent, x.SortOrder))
            .ToList();
        var totals = estimate is null
            ? CalculateTotals(lineItems)
            : new WorkOrderTotals(estimate.LaborTotal, estimate.PartsTotal, estimate.FeesTotal, estimate.DiscountTotal, estimate.Subtotal, estimate.TaxTotal, estimate.GrandTotal);

        var customers = await GetCustomersAsync(organizationId, cancellationToken);
        var vehicles = await GetVehiclesAsync(organizationId, cancellationToken);
        var configuration = await GetBusinessConfigurationAsync(organizationId, cancellationToken);
        var selectedCustomer = customers
            .Where(x => x.Id == workOrder.CustomerId)
            .Select(ToCustomerSummary)
            .FirstOrDefault();
        var selectedVehicle = vehicles
            .Where(x => x.Id == workOrder.CustomerVehicleId)
            .Select(ToVehicleSummary)
            .FirstOrDefault();

        return new WorkOrderEditor(
            workOrder.Id,
            workOrder.WorkOrderNumber,
            workOrder.RepairOrderNumber,
            workOrder.CustomerId,
            workOrder.CustomerVehicleId,
            workOrder.ServiceAdvisorEmployeeId,
            workOrder.Stage,
            workOrder.Priority,
            workOrder.PromiseDate,
            workOrder.IntakeDate,
            workOrder.RequestedService,
            workOrder.DiagnosisFindings,
            workOrder.ServiceNotes,
            workOrder.PartsAndSuppliesNotes,
            estimate?.DepositTerms ?? "No Deposit",
            estimate?.PaymentTerms,
            configuration.DefaultLaborRate,
            configuration.LaborLineItemsTaxable,
            lead is null ? null : new WorkOrderTechnicianAssignmentItem(lead.EmployeeId, lead.Role, lead.SplitPercent, lead.SortOrder),
            additional,
            lineItems,
            totals,
            customers,
            vehicles,
            selectedCustomer,
            selectedVehicle,
            await GetServiceAdvisorsAsync(organizationId, cancellationToken),
            await GetTechniciansAsync(organizationId, cancellationToken));
    }

    public async Task<Guid> SaveAsync(Guid organizationId, Guid actorUserId, SaveWorkOrderRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await ValidateCustomerAsync(organizationId, request.CustomerId, cancellationToken);
        await ValidateVehicleAsync(organizationId, request.CustomerId, request.CustomerVehicleId, cancellationToken);
        await ValidateEmployeeAsync(organizationId, request.ServiceAdvisorEmployeeId, "Service advisor", cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var isNew = request.WorkOrderId is null;
        var workOrder = isNew
            ? new WorkOrder
            {
                OrganizationId = organizationId,
                WorkOrderNumber = await GenerateWorkOrderNumberAsync(organizationId, cancellationToken),
                CustomerId = request.CustomerId,
                Stage = "Intake / Check-in",
                Priority = "Normal",
                OpenedAtUtc = now
            }
            : await dbContext.WorkOrders.IgnoreQueryFilters()
                .SingleAsync(x => x.OrganizationId == organizationId && x.Id == request.WorkOrderId, cancellationToken);

        if (isNew)
        {
            dbContext.WorkOrders.Add(workOrder);
        }

        workOrder.WorkOrderNumber = Clean(request.WorkOrderNumber) ?? workOrder.WorkOrderNumber;
        workOrder.RepairOrderNumber = Clean(request.RepairOrderNumber);
        workOrder.CustomerId = request.CustomerId;
        workOrder.CustomerVehicleId = request.CustomerVehicleId;
        workOrder.ServiceAdvisorEmployeeId = request.ServiceAdvisorEmployeeId;
        workOrder.Stage = Required(request.Stage, "Stage");
        workOrder.Priority = Required(request.Priority, "Priority");
        workOrder.PromiseDate = request.PromiseDate;
        workOrder.IntakeDate = request.IntakeDate;
        workOrder.RequestedService = Clean(request.RequestedService);
        workOrder.DiagnosisFindings = Clean(request.DiagnosisFindings);
        workOrder.ServiceNotes = Clean(request.ServiceNotes);
        workOrder.PartsAndSuppliesNotes = Clean(request.PartsAndSuppliesNotes);
        workOrder.ClosedAtUtc = workOrder.Stage is "Completed" or "Closed / Archived" ? now : null;

        await UpdateCustomerFromWorkOrderAsync(organizationId, request, cancellationToken);
        await UpdateVehicleFromWorkOrderAsync(organizationId, request, cancellationToken);
        await SaveAssignmentsAsync(organizationId, workOrder.Id, actorUserId, request, ipAddress, cancellationToken);
        await SaveEstimateAsync(organizationId, workOrder.Id, request, cancellationToken);
        AddActivity(organizationId, workOrder.Id, actorUserId, isNew ? "WorkOrderCreated" : "WorkOrderUpdated", isNew ? "Work order created" : "Work order updated", ipAddress, Snapshot(workOrder));

        await dbContext.SaveChangesAsync(cancellationToken);
        return workOrder.Id;
    }

    public async Task<WorkOrderIntakePage> GetIntakeAsync(Guid organizationId, Guid? verifiedEmployeeId, CancellationToken cancellationToken)
    {
        var advisors = await GetServiceAdvisorsAsync(organizationId, cancellationToken);
        var verifiedAdvisor = verifiedEmployeeId is null
            ? null
            : advisors.FirstOrDefault(x => x.Id == verifiedEmployeeId.Value);

        return new WorkOrderIntakePage(
            advisors,
            await GetYmmTypesAsync(cancellationToken),
            await GetYmmYearsAsync(null, cancellationToken),
            DateOnly.FromDateTime(dateTimeProvider.UtcNow.DateTime),
            verifiedAdvisor);
    }

    public async Task<WorkOrderIntakePinResult?> VerifyIntakePinAsync(Guid organizationId, string? pin, CancellationToken cancellationToken)
    {
        var cleanPin = Clean(pin);
        if (cleanPin is null || cleanPin.Length != 4 || cleanPin.Any(x => !char.IsDigit(x)))
        {
            return null;
        }

        var pinHash = HashPin(organizationId, cleanPin);
        var user = await dbContext.Users.IgnoreQueryFilters()
            .Include(x => x.Employee)
            .Where(x => x.OrganizationId == organizationId && x.PinHash == pinHash && x.IsActive && x.DeletedAtUtc == null)
            .Where(x => x.Employee != null && x.Employee.DeletedAtUtc == null && x.Employee.Status == "Active" && (x.Employee.IsServiceAdvisor || x.Employee.IsManager))
            .SingleOrDefaultAsync(cancellationToken);
        return user?.Employee is null
            ? null
            : new WorkOrderIntakePinResult(user.Employee.Id, user.Employee.DisplayName);
    }

    public async Task<WorkOrderCustomerLookupResult> LookupCustomerAsync(Guid organizationId, string? query, CancellationToken cancellationToken)
    {
        var search = Clean(query);
        if (search is null)
        {
            return new WorkOrderCustomerLookupResult([]);
        }

        var upperSearch = search.ToUpperInvariant();
        var customers = await dbContext.Customers.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.DeletedAtUtc == null)
            .Where(x =>
                x.DisplayName.ToUpper().Contains(upperSearch) ||
                (x.FirstName != null && x.FirstName.ToUpper().Contains(upperSearch)) ||
                (x.LastName != null && x.LastName.ToUpper().Contains(upperSearch)) ||
                (x.Email != null && x.Email.ToUpper().Contains(upperSearch)) ||
                (x.Phone != null && x.Phone.ToUpper().Contains(upperSearch)) ||
                (x.MobilePhone != null && x.MobilePhone.ToUpper().Contains(upperSearch)) ||
                (x.HomePhone != null && x.HomePhone.ToUpper().Contains(upperSearch)) ||
                (x.WorkPhone != null && x.WorkPhone.ToUpper().Contains(upperSearch)))
            .OrderBy(x => x.DisplayName)
            .Take(8)
            .ToListAsync(cancellationToken);
        var customerIds = customers.Select(x => x.Id).ToArray();

        var addresses = await dbContext.CustomerAddresses.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && customerIds.Contains(x.CustomerId) && x.DeletedAtUtc == null)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var addressLookup = addresses.ToLookup(x => x.CustomerId);

        var vehicles = await dbContext.CustomerVehicles.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && customerIds.Contains(x.CustomerId) && x.IsActive)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var vehicleLookup = vehicles.ToLookup(x => x.CustomerId);

        return new WorkOrderCustomerLookupResult(customers
            .Select(customer =>
            {
                var address = addressLookup[customer.Id].FirstOrDefault();
                return new WorkOrderCustomerLookupMatch(
                    customer.Id,
                    customer.DisplayName,
                    customer.FirstName,
                    customer.LastName,
                    customer.Email,
                    customer.MobilePhone ?? customer.Phone ?? customer.HomePhone ?? customer.WorkPhone,
                    customer.PreferredContactMethod,
                    customer.TaxExempt,
                    customer.TaxExemptNumber,
                    address?.Line1,
                    address?.Line2,
                    address?.City,
                    address?.Region,
                    address?.PostalCode,
                    customer.AllowSmsMarketing,
                    vehicleLookup[customer.Id]
                        .Select(vehicle => new WorkOrderCustomerVehicleMatch(
                            vehicle.Id,
                            UnitLabel(vehicle.Year, vehicle.Make, vehicle.Model, vehicle.Vin),
                            vehicle.Type,
                            vehicle.Year,
                            vehicle.Make,
                            vehicle.Model,
                            vehicle.Vin,
                            vehicle.Color,
                            vehicle.TagPlate,
                            vehicle.MileageIn ?? vehicle.Mileage,
                            vehicle.Notes))
                        .ToList());
            })
            .ToList());
    }

    public async Task<IReadOnlyCollection<string>> GetYmmTypesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.GlobalVehicles
            .Where(x => x.VehicleType != null)
            .Select(x => x.VehicleType!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<int>> GetYmmYearsAsync(string? vehicleType, CancellationToken cancellationToken)
    {
        var cleanVehicleType = Clean(vehicleType);
        return await dbContext.GlobalVehicles
            .Where(x => cleanVehicleType == null || x.VehicleType == cleanVehicleType)
            .Select(x => x.Year)
            .Distinct()
            .OrderByDescending(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetYmmMakesAsync(string? vehicleType, int year, CancellationToken cancellationToken)
    {
        var cleanVehicleType = Clean(vehicleType);
        return await dbContext.GlobalVehicles
            .Where(x => x.Year == year && (cleanVehicleType == null || x.VehicleType == cleanVehicleType))
            .Select(x => x.Make)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetYmmModelsAsync(string? vehicleType, int year, string make, CancellationToken cancellationToken)
    {
        var cleanVehicleType = Clean(vehicleType);
        var cleanMake = Required(make, "Make");
        return await dbContext.GlobalVehicles
            .Where(x => x.Year == year && x.Make == cleanMake && (cleanVehicleType == null || x.VehicleType == cleanVehicleType))
            .Select(x => x.Model)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<WorkOrderLaborItem>> SearchLaborItemsAsync(Guid organizationId, string? query, int limit, CancellationToken cancellationToken)
    {
        var search = Clean(query);
        var cappedLimit = Math.Clamp(limit, 1, 50);
        var configuration = await GetBusinessConfigurationAsync(organizationId, cancellationToken);
        var laborQuery = dbContext.LaborOperations.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.IsActive);
        if (search is not null)
        {
            var upperSearch = search.ToUpperInvariant();
            laborQuery = laborQuery.Where(x =>
                x.Code.ToUpper().Contains(upperSearch) ||
                x.Name.ToUpper().Contains(upperSearch) ||
                (x.Description != null && x.Description.ToUpper().Contains(upperSearch)) ||
                (x.ServiceCategory != null && x.ServiceCategory.ToUpper().Contains(upperSearch)));
        }

        return await laborQuery
            .OrderBy(x => x.Code)
            .Take(cappedLimit)
            .Select(x => new WorkOrderLaborItem(
                x.Id,
                x.Code,
                x.Name,
                x.Description,
                x.ServiceCategory,
                x.BaseHours,
                configuration.DefaultLaborRate,
                configuration.LaborLineItemsTaxable))
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkOrderIntakeResult> CreateFromIntakeAsync(Guid organizationId, Guid actorUserId, CreateWorkOrderIntakeRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var firstName = Required(request.FirstName ?? string.Empty, "First name");
        var lastName = Required(request.LastName ?? string.Empty, "Last name");
        var phone = Required(request.Phone ?? string.Empty, "Phone number");
        if (!request.SmsConsent)
        {
            throw new InvalidOperationException("SMS consent is required.");
        }

        var taxExemptNumber = ValidateTaxExemptNumber(request.TaxExempt, request.TaxExemptNumber);

        await ValidateEmployeeAsync(organizationId, request.ServiceAdvisorEmployeeId, "Service advisor", cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var customer = request.CustomerId is Guid customerId
            ? await dbContext.Customers.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == customerId && x.DeletedAtUtc == null, cancellationToken)
            : null;
        if (request.CustomerId is not null && customer is null)
        {
            throw new InvalidOperationException("Selected customer does not belong to this company.");
        }

        var isNewCustomer = customer is null;
        customer ??= new Customer
        {
            OrganizationId = organizationId,
            CustomerNumber = await GenerateCustomerNumberAsync(organizationId, cancellationToken),
            DisplayName = BuildDisplayName(firstName, null, lastName, null, request.Email, phone),
            CustomerSince = DateOnly.FromDateTime(now.DateTime)
        };

        if (isNewCustomer)
        {
            dbContext.Customers.Add(customer);
        }

        customer.DisplayName = BuildDisplayName(firstName, null, lastName, null, request.Email, phone);
        customer.FirstName = firstName;
        customer.LastName = lastName;
        customer.Email = Clean(request.Email);
        customer.Phone = phone;
        customer.MobilePhone = phone;
        customer.CustomerType = "Individual";
        customer.Status = "Active";
        customer.LifecycleStage = "Active";
        customer.Source = "Service Intake";
        customer.PreferredContactMethod = Clean(request.PreferredContactMethod) ?? "Phone";
        customer.AllowEmailMarketing = true;
        customer.AllowSmsMarketing = request.SmsConsent;
        customer.AllowPhoneCalls = true;
        customer.TaxExempt = request.TaxExempt;
        customer.TaxExemptNumber = taxExemptNumber;
        customer.IsActive = true;

        await UpsertPrimaryAddressAsync(
            organizationId,
            customer.Id,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.Region,
            request.PostalCode,
            "US",
            cancellationToken);

        CustomerVehicle? vehicle = null;
        if (!request.BypassVehicleSelection)
        {
            var type = Required(request.Type, "Vehicle type");
            var year = request.Year ?? throw new InvalidOperationException("Vehicle year is required unless bypassed.");
            var make = Required(request.Make ?? string.Empty, "Vehicle make");
            var model = Required(request.Model ?? string.Empty, "Vehicle model");

            vehicle = request.CustomerVehicleId is Guid vehicleId
                ? await dbContext.CustomerVehicles.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == vehicleId && x.CustomerId == customer.Id, cancellationToken)
                : null;

            if (request.CustomerVehicleId is not null && vehicle is null)
            {
                throw new InvalidOperationException("Selected saved vehicle does not belong to this customer.");
            }

            vehicle ??= new CustomerVehicle
            {
                OrganizationId = organizationId,
                CustomerId = customer.Id
            };

            if (vehicle.Id == Guid.Empty || !await dbContext.CustomerVehicles.IgnoreQueryFilters().AnyAsync(x => x.Id == vehicle.Id, cancellationToken))
            {
                dbContext.CustomerVehicles.Add(vehicle);
            }

            vehicle.Type = type;
            vehicle.Year = year;
            vehicle.Make = make;
            vehicle.Model = model;
            vehicle.Vin = Clean(request.Vin);
            vehicle.Color = Clean(request.Color);
            vehicle.TagPlate = Clean(request.TagPlate);
            vehicle.MileageIn = request.MileageIn;
            vehicle.Mileage = request.MileageIn;
            vehicle.Notes = Clean(request.EngineSerialNotes);
            vehicle.IsActive = true;
        }

        var workOrder = new WorkOrder
        {
            OrganizationId = organizationId,
            WorkOrderNumber = await GenerateWorkOrderNumberAsync(organizationId, cancellationToken),
            CustomerId = customer.Id,
            CustomerVehicleId = vehicle?.Id,
            ServiceAdvisorEmployeeId = request.ServiceAdvisorEmployeeId,
            Stage = "Intake / Check-in",
            Priority = Required(request.Priority, "Priority"),
            PromiseDate = request.PromiseDate,
            IntakeDate = DateOnly.FromDateTime(now.DateTime),
            RequestedService = Required(request.RequestedService ?? string.Empty, "Requested service"),
            OpenedAtUtc = now
        };
        dbContext.WorkOrders.Add(workOrder);

        AddActivity(
            organizationId,
            workOrder.Id,
            actorUserId,
            "WorkOrderIntakeCreated",
            "Work order intake created",
            ipAddress,
            new
            {
                workOrder.WorkOrderNumber,
                Customer = customer.DisplayName,
                CustomerCreated = isNewCustomer,
                Vehicle = vehicle is null ? null : UnitLabel(vehicle.Year, vehicle.Make, vehicle.Model, vehicle.Vin),
                request.BypassVehicleSelection
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new WorkOrderIntakeResult(workOrder.Id, customer.Id, vehicle?.Id);
    }

    public async Task AddAttachmentsAsync(Guid organizationId, Guid actorUserId, AddWorkOrderAttachmentRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var workOrder = await dbContext.WorkOrders.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == request.WorkOrderId, cancellationToken);
        var uploads = request.Attachments.Where(x => !string.IsNullOrWhiteSpace(x.FileName)).ToList();
        if (uploads.Count == 0)
        {
            return;
        }

        foreach (var upload in uploads)
        {
            dbContext.WorkOrderAttachments.Add(new WorkOrderAttachment
            {
                OrganizationId = organizationId,
                WorkOrderId = workOrder.Id,
                CustomerId = workOrder.CustomerId,
                CustomerVehicleId = workOrder.CustomerVehicleId,
                AttachmentType = Required(upload.AttachmentType, "Attachment type"),
                FileName = Required(upload.FileName, "File name"),
                Url = Clean(upload.Url),
                ContentType = Clean(upload.ContentType),
                UploadedAtUtc = dateTimeProvider.UtcNow
            });
        }

        AddActivity(organizationId, workOrder.Id, actorUserId, "WorkOrderMediaAdded", "Work order media added", ipAddress, new { Count = uploads.Count });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateCustomerFromWorkOrderAsync(Guid organizationId, SaveWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == request.CustomerId && x.DeletedAtUtc == null, cancellationToken);
        var displayName = Clean(request.CustomerDisplayName);
        var phone = Clean(request.CustomerPhone);

        if (displayName is not null)
        {
            customer.DisplayName = displayName;
        }

        customer.Email = Clean(request.CustomerEmail);
        customer.Phone = phone;
        customer.MobilePhone = phone;
        customer.PreferredContactMethod = Clean(request.CustomerPreferredContactMethod) ?? customer.PreferredContactMethod;
        customer.AllowSmsMarketing = request.CustomerSmsConsent;
        customer.TaxExempt = request.CustomerTaxExempt;
        customer.TaxExemptNumber = ValidateTaxExemptNumber(request.CustomerTaxExempt, request.CustomerTaxExemptNumber);

        await UpsertPrimaryAddressAsync(
            organizationId,
            customer.Id,
            request.CustomerAddressLine1,
            request.CustomerAddressLine2,
            request.CustomerCity,
            request.CustomerRegion,
            request.CustomerPostalCode,
            "US",
            cancellationToken);
    }

    private async Task UpdateVehicleFromWorkOrderAsync(Guid organizationId, SaveWorkOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerVehicleId is not Guid vehicleId)
        {
            return;
        }

        var vehicle = await dbContext.CustomerVehicles.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.CustomerId == request.CustomerId && x.Id == vehicleId, cancellationToken);
        vehicle.Type = Clean(request.UnitType) ?? vehicle.Type;
        vehicle.Year = request.UnitYear;
        vehicle.Make = Clean(request.UnitMake);
        vehicle.Model = Clean(request.UnitModel);
        vehicle.Vin = Clean(request.UnitVin);
        vehicle.Color = Clean(request.UnitColor);
        vehicle.TagPlate = Clean(request.UnitTagPlate);
        vehicle.MileageIn = request.UnitMileageIn;
        vehicle.Mileage = request.UnitMileageIn;
        vehicle.Notes = Clean(request.UnitNotes);
        vehicle.IsActive = true;
    }

    private async Task SaveAssignmentsAsync(Guid organizationId, Guid workOrderId, Guid actorUserId, SaveWorkOrderRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var existing = await dbContext.WorkOrderTechnicianAssignments.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var before = existing.Select(ToAssignmentSnapshot).ToList();

        var assignments = BuildAssignments(organizationId, workOrderId, request);

        foreach (var assignment in assignments)
        {
            await ValidateTechnicianAsync(organizationId, assignment.EmployeeId, cancellationToken);
            if (assignment.SplitPercent < 1m || assignment.SplitPercent > 100m)
            {
                throw new InvalidOperationException("Each technician split must be between 1% and 100%.");
            }
        }

        var duplicateTechnician = assignments
            .GroupBy(x => x.EmployeeId)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateTechnician is not null)
        {
            throw new InvalidOperationException("A technician can only be added to a work order split once.");
        }

        var splitTotal = assignments.Sum(x => x.SplitPercent);
        if (assignments.Count > 0 && splitTotal != 100m)
        {
            throw new InvalidOperationException("Technician split total must equal 100%.");
        }

        var after = assignments.Select(ToAssignmentSnapshot).ToList();
        if (!AssignmentSnapshotsEqual(before, after))
        {
            AddActivity(
                organizationId,
                workOrderId,
                actorUserId,
                "WorkOrderTechnicianSplitsChanged",
                "Technician splits updated",
                ipAddress,
                new { Before = before, After = after });
        }

        dbContext.WorkOrderTechnicianAssignments.RemoveRange(existing);
        dbContext.WorkOrderTechnicianAssignments.AddRange(assignments);
    }

    private async Task SaveEstimateAsync(Guid organizationId, Guid workOrderId, SaveWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var configuration = await GetBusinessConfigurationAsync(organizationId, cancellationToken);
        var estimate = await dbContext.Estimates.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.WorkOrderId == workOrderId)
            .OrderByDescending(x => x.CreatedForCustomerAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (estimate is null)
        {
            estimate = new Estimate
            {
                OrganizationId = organizationId,
                WorkOrderId = workOrderId,
                EstimateNumber = await GenerateEstimateNumberAsync(organizationId, cancellationToken),
                Status = "Draft",
                CreatedForCustomerAtUtc = dateTimeProvider.UtcNow
            };
            dbContext.Estimates.Add(estimate);
        }

        var existingLineItems = await dbContext.EstimateLineItems.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.EstimateId == estimate.Id)
            .ToListAsync(cancellationToken);
        dbContext.EstimateLineItems.RemoveRange(existingLineItems);

        var lineItems = request.LineItems
            .Where(x => !string.IsNullOrWhiteSpace(x.Description))
            .Select((x, index) => ToLineItem(organizationId, workOrderId, estimate.Id, x, index, configuration.LaborLineItemsTaxable))
            .ToList();
        dbContext.EstimateLineItems.AddRange(lineItems);

        var totals = CalculateTotals(lineItems.Select(x => new EstimateLineItemEditor(
            x.Id,
            x.LineType,
            x.Description,
            x.Notes,
            x.Sku,
            x.Quantity,
            x.Rate,
            x.DiscountAmount,
            x.DiscountPercent,
            x.LineTotal,
            x.IsTaxable,
            x.IsDeclined,
            x.IsDone,
            x.SortOrder)));

        estimate.DepositTerms = Required(request.DepositTerms, "Deposit terms");
        estimate.PaymentTerms = Clean(request.PaymentTerms);
        estimate.LaborTotal = totals.LaborTotal;
        estimate.PartsTotal = totals.PartsTotal;
        estimate.FeesTotal = totals.FeesTotal;
        estimate.DiscountTotal = totals.DiscountTotal;
        estimate.Subtotal = totals.Subtotal;
        estimate.TaxTotal = totals.TaxTotal;
        estimate.GrandTotal = totals.EstimateTotal;
    }

    private async Task<IReadOnlyCollection<WorkOrderCustomerOption>> GetCustomersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var customers = await dbContext.Customers.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.DeletedAtUtc == null)
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
        var customerIds = customers.Select(x => x.Id).ToArray();
        var addresses = await dbContext.CustomerAddresses.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && customerIds.Contains(x.CustomerId) && x.DeletedAtUtc == null)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var addressLookup = addresses.ToLookup(x => x.CustomerId);

        return customers
            .Select(x =>
            {
                var address = addressLookup[x.Id].FirstOrDefault();
                return new WorkOrderCustomerOption(
                    x.Id,
                    x.DisplayName,
                    x.Email,
                    x.MobilePhone ?? x.Phone,
                    x.PreferredContactMethod,
                    x.AllowSmsMarketing,
                    x.TaxExempt,
                    x.TaxExemptNumber,
                    address?.Line1,
                    address?.Line2,
                    address?.City,
                    address?.Region,
                    address?.PostalCode);
            })
            .ToList();
    }

    private async Task<IReadOnlyCollection<WorkOrderVehicleOption>> GetVehiclesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await dbContext.CustomerVehicles.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderByDescending(x => x.Year)
            .ThenBy(x => x.Make)
            .ThenBy(x => x.Model)
            .Select(x => new WorkOrderVehicleOption(
                x.Id,
                x.CustomerId,
                UnitLabel(x.Year, x.Make, x.Model, x.Vin),
                x.Type,
                x.Year,
                x.Make,
                x.Model,
                x.Vin,
                x.Color,
                x.TagPlate,
                x.MileageIn ?? x.Mileage,
                x.Notes))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<WorkOrderEmployeeOption>> GetServiceAdvisorsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await dbContext.Employees.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.DeletedAtUtc == null && x.Status == "Active" && (x.IsServiceAdvisor || x.IsManager))
            .OrderBy(x => x.DisplayName)
            .Select(x => new WorkOrderEmployeeOption(x.Id, x.DisplayName))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<WorkOrderEmployeeOption>> GetTechniciansAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await dbContext.Employees.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.DeletedAtUtc == null && x.Status == "Active" && x.IsTechnician)
            .OrderBy(x => x.DisplayName)
            .Select(x => new WorkOrderEmployeeOption(x.Id, x.DisplayName))
            .ToListAsync(cancellationToken);
    }

    private async Task ValidateCustomerAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Customers.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.Id == customerId && x.DeletedAtUtc == null, cancellationToken))
        {
            throw new InvalidOperationException("Customer is required.");
        }
    }

    private async Task ValidateVehicleAsync(Guid organizationId, Guid customerId, Guid? vehicleId, CancellationToken cancellationToken)
    {
        if (vehicleId is null)
        {
            return;
        }

        if (!await dbContext.CustomerVehicles.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.CustomerId == customerId && x.Id == vehicleId, cancellationToken))
        {
            throw new InvalidOperationException("Selected unit does not belong to the selected customer.");
        }
    }

    private async Task ValidateEmployeeAsync(Guid organizationId, Guid? employeeId, string fieldName, CancellationToken cancellationToken)
    {
        if (employeeId is null)
        {
            return;
        }

        if (!await dbContext.Employees.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.Id == employeeId && x.DeletedAtUtc == null, cancellationToken))
        {
            throw new InvalidOperationException($"{fieldName} does not belong to this company.");
        }
    }

    private async Task ValidateTechnicianAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Employees.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.Id == employeeId && x.DeletedAtUtc == null && x.Status == "Active" && x.IsTechnician, cancellationToken))
        {
            throw new InvalidOperationException("Technician does not belong to this company or is not an active technician.");
        }
    }

    private async Task<string> GenerateWorkOrderNumberAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var numbers = await dbContext.WorkOrders.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.WorkOrderNumber)
            .ToListAsync(cancellationToken);

        return NextNumber(numbers, "WO");
    }

    private async Task<string> GenerateEstimateNumberAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var numbers = await dbContext.Estimates.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.EstimateNumber)
            .ToListAsync(cancellationToken);

        return NextNumber(numbers, "EST");
    }

    private async Task<string> GenerateCustomerNumberAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var numbers = await dbContext.Customers.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.CustomerNumber != null)
            .Select(x => x.CustomerNumber!)
            .ToListAsync(cancellationToken);

        return NextNumber(numbers, "CUS");
    }

    private async Task UpsertPrimaryAddressAsync(
        Guid organizationId,
        Guid customerId,
        string? line1,
        string? line2,
        string? city,
        string? region,
        string? postalCode,
        string? country,
        CancellationToken cancellationToken)
    {
        var hasAddressValue = !string.IsNullOrWhiteSpace(line1) ||
            !string.IsNullOrWhiteSpace(line2) ||
            !string.IsNullOrWhiteSpace(city) ||
            !string.IsNullOrWhiteSpace(region) ||
            !string.IsNullOrWhiteSpace(postalCode);

        var address = await dbContext.CustomerAddresses.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.CustomerId == customerId && x.DeletedAtUtc == null)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (address is null && !hasAddressValue)
        {
            return;
        }

        if (address is null)
        {
            address = new CustomerAddress
            {
                OrganizationId = organizationId,
                CustomerId = customerId,
                AddressType = "Primary",
                IsPrimary = true,
                IsBilling = true,
                IsShipping = true
            };
            dbContext.CustomerAddresses.Add(address);
        }

        address.Line1 = Clean(line1);
        address.Line2 = Clean(line2);
        address.City = Clean(city);
        address.Region = Clean(region);
        address.PostalCode = Clean(postalCode);
        address.Country = Clean(country) ?? "US";
        address.IsPrimary = true;
    }

    private void AddActivity(Guid organizationId, Guid workOrderId, Guid actorUserId, string eventType, string summary, string? ipAddress, object payload)
    {
        var now = dateTimeProvider.UtcNow;
        var payloadJson = JsonSerializer.Serialize(payload);
        dbContext.AuditLogs.Add(new AuditLog
        {
            OrganizationId = organizationId,
            UserId = actorUserId.ToString(),
            Action = eventType,
            EntityName = "WorkOrder",
            EntityId = workOrderId.ToString(),
            ChangesJson = payloadJson,
            OccurredAtUtc = now
        });
        dbContext.TimelineEvents.Add(new TimelineEvent
        {
            OrganizationId = organizationId,
            EntityType = "WorkOrder",
            EntityId = workOrderId.ToString(),
            EventType = eventType,
            ActorUserId = actorUserId.ToString(),
            Summary = summary,
            OccurredAtUtc = now,
            PayloadJson = payloadJson
        });
    }

    private static List<WorkOrderTechnicianAssignment> BuildAssignments(Guid organizationId, Guid workOrderId, SaveWorkOrderRequest request)
    {
        var requested = RequestedAssignments(request);
        if (requested.Count == 0)
        {
            return [];
        }

        var leadIndex = requested
            .Select((x, index) => new { Assignment = x, Index = index })
            .OrderByDescending(x => x.Assignment.SplitPercent ?? 0m)
            .ThenBy(x => x.Index)
            .First()
            .Index;

        return requested
            .Select((x, index) => new WorkOrderTechnicianAssignment
            {
                OrganizationId = organizationId,
                WorkOrderId = workOrderId,
                EmployeeId = x.EmployeeId!.Value,
                Role = index == leadIndex ? "Lead Technician" : "Technician",
                SplitPercent = x.SplitPercent ?? 0m,
                SortOrder = index
            })
            .ToList();
    }

    private static List<SaveWorkOrderTechnicianAssignmentRequest> RequestedAssignments(SaveWorkOrderRequest request)
    {
        var requested = request.TechnicianAssignments
            .Where(x => x.EmployeeId is not null || x.SplitPercent is not null)
            .ToList();
        if (requested.Count == 0)
        {
            AddLegacyAssignment(requested, request.LeadTechnicianEmployeeId, request.LeadTechnicianSplitPercent);
            AddLegacyAssignment(requested, request.AdditionalTechnician1EmployeeId, request.AdditionalTechnician1SplitPercent);
            AddLegacyAssignment(requested, request.AdditionalTechnician2EmployeeId, request.AdditionalTechnician2SplitPercent);
        }

        if (requested.Any(x => x.EmployeeId is null))
        {
            throw new InvalidOperationException("Technician is required for each split.");
        }

        return requested;
    }

    private static void AddLegacyAssignment(List<SaveWorkOrderTechnicianAssignmentRequest> requested, Guid? employeeId, decimal? splitPercent)
    {
        if (employeeId is null && splitPercent is null)
        {
            return;
        }

        requested.Add(new SaveWorkOrderTechnicianAssignmentRequest
        {
            EmployeeId = employeeId,
            SplitPercent = splitPercent
        });
    }

    private static TechnicianAssignmentSnapshot ToAssignmentSnapshot(WorkOrderTechnicianAssignment assignment)
    {
        return new TechnicianAssignmentSnapshot(assignment.EmployeeId, assignment.Role, assignment.SplitPercent, assignment.SortOrder);
    }

    private static bool AssignmentSnapshotsEqual(IReadOnlyCollection<TechnicianAssignmentSnapshot> before, IReadOnlyCollection<TechnicianAssignmentSnapshot> after)
    {
        return before.Count == after.Count && before.Zip(after).All(x => x.First == x.Second);
    }

    private static EstimateLineItem ToLineItem(Guid organizationId, Guid workOrderId, Guid estimateId, SaveEstimateLineItemRequest request, int sortOrder, bool laborLineItemsTaxable)
    {
        var lineType = Required(request.LineType, "Line type");
        var quantity = request.Quantity <= 0 ? 1m : request.Quantity;
        var rate = request.Rate < 0 ? 0m : request.Rate;
        var gross = quantity * rate;
        var discountPercentAmount = gross * Math.Clamp(request.DiscountPercent, 0m, 100m) / 100m;
        var discountAmount = Math.Clamp(request.DiscountAmount, 0m, gross);
        var lineTotal = Math.Max(0m, gross - discountPercentAmount - discountAmount);

        return new EstimateLineItem
        {
            OrganizationId = organizationId,
            WorkOrderId = workOrderId,
            EstimateId = estimateId,
            LineType = lineType,
            Description = Required(request.Description ?? string.Empty, "Line description"),
            Notes = Clean(request.Notes),
            Sku = Clean(request.Sku),
            Quantity = quantity,
            Rate = rate,
            DiscountAmount = discountAmount,
            DiscountPercent = Math.Clamp(request.DiscountPercent, 0m, 100m),
            LineTotal = lineTotal,
            IsTaxable = lineType.Equals("Labor", StringComparison.OrdinalIgnoreCase) ? laborLineItemsTaxable : request.IsTaxable,
            IsDeclined = request.IsDeclined,
            IsDone = request.IsDone,
            SortOrder = sortOrder
        };
    }

    private static WorkOrderTotals CalculateTotals(IEnumerable<EstimateLineItemEditor> lineItems)
    {
        var active = lineItems.Where(x => !x.IsDeclined && !string.IsNullOrWhiteSpace(x.Description)).ToArray();
        var labor = active.Where(x => x.LineType.Equals("Labor", StringComparison.OrdinalIgnoreCase)).Sum(x => x.LineTotal);
        var parts = active.Where(x => x.LineType.Equals("Parts", StringComparison.OrdinalIgnoreCase)).Sum(x => x.LineTotal);
        var fees = active.Where(x => x.LineType.Equals("Fees / Diagnostics", StringComparison.OrdinalIgnoreCase)).Sum(x => x.LineTotal);
        var discount = active.Sum(x => x.DiscountAmount + ((x.Quantity * x.Rate) * x.DiscountPercent / 100m));
        var subtotal = active.Sum(x => x.LineTotal);
        return new WorkOrderTotals(labor, parts, fees, discount, subtotal, 0m, subtotal);
    }

    private async Task<BusinessConfiguration> GetBusinessConfigurationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var configuration = await dbContext.BusinessConfigurations.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (configuration is not null)
        {
            return configuration;
        }

        configuration = new BusinessConfiguration
        {
            OrganizationId = organizationId,
            LaborLineItemsTaxable = true
        };
        dbContext.BusinessConfigurations.Add(configuration);
        return configuration;
    }

    private static EstimateLineItemEditor BlankLineItem(bool laborLineItemsTaxable)
    {
        return new EstimateLineItemEditor(null, "Labor", null, null, null, 1m, 0m, 0m, 0m, 0m, laborLineItemsTaxable, false, false, 0);
    }

    private static WorkOrderCustomerSummary ToCustomerSummary(WorkOrderCustomerOption customer)
    {
        return new WorkOrderCustomerSummary(
            customer.Id,
            customer.DisplayName,
            customer.Email,
            customer.Phone,
            customer.PreferredContactMethod,
            customer.AllowSmsMarketing,
            customer.TaxExempt,
            customer.TaxExemptNumber,
            customer.AddressLine1,
            customer.AddressLine2,
            customer.City,
            customer.Region,
            customer.PostalCode);
    }

    private static WorkOrderVehicleSummary ToVehicleSummary(WorkOrderVehicleOption vehicle)
    {
        return new WorkOrderVehicleSummary(
            vehicle.Id,
            vehicle.CustomerId,
            vehicle.Label,
            vehicle.Type,
            vehicle.Year,
            vehicle.Make,
            vehicle.Model,
            vehicle.Vin,
            vehicle.Color,
            vehicle.TagPlate,
            vehicle.MileageIn,
            vehicle.Notes);
    }

    private static string NextNumber(IEnumerable<string> existingNumbers, string prefix)
    {
        var numberPrefix = $"{prefix}-";
        var next = existingNumbers
            .Where(x => x.StartsWith(numberPrefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(x[numberPrefix.Length..], out _))
            .Select(x => int.Parse(x[numberPrefix.Length..]))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}-{next:000000}";
    }

    private static object Snapshot(WorkOrder workOrder) => new
    {
        workOrder.WorkOrderNumber,
        workOrder.RepairOrderNumber,
        workOrder.CustomerId,
        workOrder.CustomerVehicleId,
        workOrder.ServiceAdvisorEmployeeId,
        workOrder.Stage,
        workOrder.Priority,
        workOrder.PromiseDate,
        workOrder.IntakeDate
    };

    private static string BuildDisplayName(string? firstName, string? middleName, string? lastName, string? companyName, string? email, string? phone)
    {
        var personName = string.Join(" ", new[] { Clean(firstName), Clean(middleName), Clean(lastName) }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return Clean(companyName) ?? Clean(personName) ?? Clean(email) ?? Clean(phone) ?? "New Customer";
    }

    private static string? ValidateTaxExemptNumber(bool taxExempt, string? taxExemptNumber)
    {
        var cleanNumber = Clean(taxExemptNumber);
        if (taxExempt && string.IsNullOrWhiteSpace(cleanNumber))
        {
            throw new InvalidOperationException("Tax exempt number is required when Tax Exempt is checked.");
        }

        return taxExempt ? cleanNumber : null;
    }

    private static string UnitLabel(int? year, string? make, string? model, string? vin)
    {
        var ymm = string.Join(" ", new[] { year?.ToString(), make, model }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(vin) ? ymm : $"{ymm} ({vin})";
    }

    private static bool Contains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string Required(string value, string fieldName) => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{fieldName} is required.") : value.Trim();

    private static string HashPin(Guid organizationId, string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{organizationId:N}:{pin}"));
        return Convert.ToHexString(bytes);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record TechnicianAssignmentSnapshot(Guid EmployeeId, string Role, decimal SplitPercent, int SortOrder);
}

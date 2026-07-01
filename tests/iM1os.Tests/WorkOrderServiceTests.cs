using System.Security.Cryptography;
using System.Text;
using iM1os.Application.WorkOrders;
using iM1os.Domain.Customers;
using iM1os.Domain.Employees;
using iM1os.Domain.GlobalCatalog;
using iM1os.Domain.Identity;
using iM1os.Domain.Service;
using iM1os.Domain.Tenancy;
using iM1os.Domain.Vehicles;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class WorkOrderServiceTests
{
    [Fact]
    public async Task Workspace_only_returns_work_orders_for_requested_company()
    {
        await using var dbContext = CreateContext();
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var customerA = new Customer { OrganizationId = organizationA, DisplayName = "A Customer" };
        var customerB = new Customer { OrganizationId = organizationB, DisplayName = "B Customer" };
        dbContext.Customers.AddRange(customerA, customerB);
        dbContext.WorkOrders.AddRange(
            new WorkOrder
            {
                OrganizationId = organizationA,
                WorkOrderNumber = "WO-A",
                CustomerId = customerA.Id,
                Stage = "Intake / Check-in",
                Priority = "Normal",
                OpenedAtUtc = DateTimeOffset.UtcNow
            },
            new WorkOrder
            {
                OrganizationId = organizationB,
                WorkOrderNumber = "WO-B",
                CustomerId = customerB.Id,
                Stage = "Intake / Check-in",
                Priority = "Normal",
                OpenedAtUtc = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var service = new WorkOrderService(dbContext, new SystemClock());
        var workspace = await service.GetWorkspaceAsync(organizationA, new WorkOrderSearchRequest(null, null), CancellationToken.None);

        var row = Assert.Single(workspace.WorkOrders);
        Assert.Equal("WO-A", row.WorkOrderNumber);
        Assert.Equal("A Customer", row.CustomerName);
    }

    [Fact]
    public async Task Save_rejects_customer_unit_and_employee_from_other_company()
    {
        await using var dbContext = CreateContext();
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var customerA = new Customer { OrganizationId = organizationA, DisplayName = "A Customer" };
        var customerB = new Customer { OrganizationId = organizationB, DisplayName = "B Customer" };
        var unitB = new CustomerVehicle { OrganizationId = organizationB, CustomerId = customerB.Id, Type = "Street", Year = 2005, Make = "Triumph", Model = "Bonneville" };
        var employeeB = new Employee { OrganizationId = organizationB, DisplayName = "Other Advisor", Status = "Active", IsServiceAdvisor = true };
        dbContext.Customers.AddRange(customerA, customerB);
        dbContext.CustomerVehicles.Add(unitB);
        dbContext.Employees.Add(employeeB);
        await dbContext.SaveChangesAsync();

        var service = new WorkOrderService(dbContext, new SystemClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            organizationA,
            Guid.NewGuid(),
            new SaveWorkOrderRequest
            {
                CustomerId = customerB.Id,
                Stage = "Intake / Check-in",
                Priority = "Normal"
            },
            null,
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            organizationA,
            Guid.NewGuid(),
            new SaveWorkOrderRequest
            {
                CustomerId = customerA.Id,
                CustomerVehicleId = unitB.Id,
                Stage = "Intake / Check-in",
                Priority = "Normal"
            },
            null,
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            organizationA,
            Guid.NewGuid(),
            new SaveWorkOrderRequest
            {
                CustomerId = customerA.Id,
                ServiceAdvisorEmployeeId = employeeB.Id,
                Stage = "Intake / Check-in",
                Priority = "Normal"
            },
            null,
            CancellationToken.None));
    }

    [Fact]
    public async Task Save_creates_work_order_estimate_line_items_and_technician_assignments_for_company()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var customer = new Customer { OrganizationId = organizationId, DisplayName = "Ed Blazer" };
        var unit = new CustomerVehicle { OrganizationId = organizationId, CustomerId = customer.Id, Type = "Street", Year = 2005, Make = "Triumph", Model = "Bonneville T100" };
        var advisor = new Employee { OrganizationId = organizationId, DisplayName = "Bradley Molen", Status = "Active", IsServiceAdvisor = true };
        var lead = new Employee { OrganizationId = organizationId, DisplayName = "Jeremy Aultman", Status = "Active", IsTechnician = true };
        var additional = new Employee { OrganizationId = organizationId, DisplayName = "Ryan Pease", Status = "Active", IsTechnician = true };
        var helper = new Employee { OrganizationId = organizationId, DisplayName = "Taylor Smith", Status = "Active", IsTechnician = true };
        dbContext.Customers.Add(customer);
        dbContext.CustomerVehicles.Add(unit);
        dbContext.Employees.AddRange(advisor, lead, additional, helper);
        await dbContext.SaveChangesAsync();

        var service = new WorkOrderService(dbContext, new SystemClock());
        var actorUserId = Guid.NewGuid();
        var workOrderId = await service.SaveAsync(
            organizationId,
            actorUserId,
            new SaveWorkOrderRequest
            {
                RepairOrderNumber = "RO-277255",
                CustomerId = customer.Id,
                CustomerVehicleId = unit.Id,
                ServiceAdvisorEmployeeId = advisor.Id,
                Stage = "Intake / Check-in",
                Priority = "Normal",
                RequestedService = "Carburetors need rebuilt",
                TechnicianAssignments =
                [
                    new SaveWorkOrderTechnicianAssignmentRequest { EmployeeId = lead.Id, SplitPercent = 25m },
                    new SaveWorkOrderTechnicianAssignmentRequest { EmployeeId = additional.Id, SplitPercent = 55m },
                    new SaveWorkOrderTechnicianAssignmentRequest { EmployeeId = helper.Id, SplitPercent = 20m }
                ],
                LineItems =
                [
                    new SaveEstimateLineItemRequest
                    {
                        LineType = "Labor",
                        Description = "Carb clean and rebuild",
                        Quantity = 2m,
                        Rate = 125m
                    }
                ]
            },
            null,
            CancellationToken.None);

        var workOrder = await dbContext.WorkOrders.IgnoreQueryFilters().SingleAsync(x => x.Id == workOrderId);
        var estimate = await dbContext.Estimates.IgnoreQueryFilters().SingleAsync(x => x.WorkOrderId == workOrderId);

        Assert.Equal(organizationId, workOrder.OrganizationId);
        Assert.Equal("RO-277255", workOrder.RepairOrderNumber);
        Assert.Equal(250m, estimate.GrandTotal);
        var assignments = await dbContext.WorkOrderTechnicianAssignments.IgnoreQueryFilters()
            .Where(x => x.WorkOrderId == workOrderId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();
        Assert.Equal(3, assignments.Count);
        Assert.Equal(additional.Id, assignments.Single(x => x.Role == "Lead Technician").EmployeeId);
        Assert.All(assignments.Where(x => x.EmployeeId != additional.Id), x => Assert.Equal("Technician", x.Role));
        Assert.Equal(1, await dbContext.EstimateLineItems.IgnoreQueryFilters().CountAsync(x => x.WorkOrderId == workOrderId));
        Assert.True(await dbContext.AuditLogs.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.EntityId == workOrderId.ToString() && x.Action == "WorkOrderTechnicianSplitsChanged"));
        Assert.True(await dbContext.TimelineEvents.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.EntityId == workOrderId.ToString() && x.EventType == "WorkOrderTechnicianSplitsChanged"));
    }

    [Fact]
    public async Task Save_rejects_technician_splits_that_do_not_total_100_percent()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var customer = new Customer { OrganizationId = organizationId, DisplayName = "Ed Blazer" };
        var technicianA = new Employee { OrganizationId = organizationId, DisplayName = "Jeremy Aultman", Status = "Active", IsTechnician = true };
        var technicianB = new Employee { OrganizationId = organizationId, DisplayName = "Ryan Pease", Status = "Active", IsTechnician = true };
        dbContext.Customers.Add(customer);
        dbContext.Employees.AddRange(technicianA, technicianB);
        await dbContext.SaveChangesAsync();

        var service = new WorkOrderService(dbContext, new SystemClock());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            organizationId,
            Guid.NewGuid(),
            new SaveWorkOrderRequest
            {
                CustomerId = customer.Id,
                Stage = "Intake / Check-in",
                Priority = "Normal",
                TechnicianAssignments =
                [
                    new SaveWorkOrderTechnicianAssignmentRequest { EmployeeId = technicianA.Id, SplitPercent = 60m },
                    new SaveWorkOrderTechnicianAssignmentRequest { EmployeeId = technicianB.Id, SplitPercent = 30m }
                ]
            },
            null,
            CancellationToken.None));

        Assert.Equal("Technician split total must equal 100%.", exception.Message);
        Assert.False(await dbContext.WorkOrderTechnicianAssignments.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId));
    }

    [Fact]
    public async Task Save_logs_technician_split_changes_when_assignments_are_edited()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var customer = new Customer { OrganizationId = organizationId, DisplayName = "Ed Blazer" };
        var technicianA = new Employee { OrganizationId = organizationId, DisplayName = "Jeremy Aultman", Status = "Active", IsTechnician = true };
        var technicianB = new Employee { OrganizationId = organizationId, DisplayName = "Ryan Pease", Status = "Active", IsTechnician = true };
        dbContext.Customers.Add(customer);
        dbContext.Employees.AddRange(technicianA, technicianB);
        await dbContext.SaveChangesAsync();

        var service = new WorkOrderService(dbContext, new SystemClock());
        var actorUserId = Guid.NewGuid();
        var workOrderId = await service.SaveAsync(
            organizationId,
            actorUserId,
            new SaveWorkOrderRequest
            {
                CustomerId = customer.Id,
                Stage = "Intake / Check-in",
                Priority = "Normal",
                TechnicianAssignments =
                [
                    new SaveWorkOrderTechnicianAssignmentRequest { EmployeeId = technicianA.Id, SplitPercent = 100m }
                ]
            },
            null,
            CancellationToken.None);

        await service.SaveAsync(
            organizationId,
            actorUserId,
            new SaveWorkOrderRequest
            {
                WorkOrderId = workOrderId,
                CustomerId = customer.Id,
                Stage = "In Progress",
                Priority = "Normal",
                TechnicianAssignments =
                [
                    new SaveWorkOrderTechnicianAssignmentRequest { EmployeeId = technicianA.Id, SplitPercent = 40m },
                    new SaveWorkOrderTechnicianAssignmentRequest { EmployeeId = technicianB.Id, SplitPercent = 60m }
                ]
            },
            null,
            CancellationToken.None);

        var assignments = await dbContext.WorkOrderTechnicianAssignments.IgnoreQueryFilters()
            .Where(x => x.WorkOrderId == workOrderId)
            .ToListAsync();

        Assert.Equal(2, assignments.Count);
        Assert.Equal(technicianB.Id, assignments.Single(x => x.Role == "Lead Technician").EmployeeId);
        Assert.True(await dbContext.AuditLogs.IgnoreQueryFilters().CountAsync(x => x.EntityId == workOrderId.ToString() && x.Action == "WorkOrderTechnicianSplitsChanged") >= 2);
    }

    [Fact]
    public async Task Save_forces_labor_taxable_from_company_configuration()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var customer = new Customer { OrganizationId = organizationId, DisplayName = "Ed Blazer" };
        dbContext.Customers.Add(customer);
        dbContext.BusinessConfigurations.Add(new BusinessConfiguration
        {
            OrganizationId = organizationId,
            LaborLineItemsTaxable = false
        });
        await dbContext.SaveChangesAsync();

        var service = new WorkOrderService(dbContext, new SystemClock());
        var workOrderId = await service.SaveAsync(
            organizationId,
            Guid.NewGuid(),
            new SaveWorkOrderRequest
            {
                CustomerId = customer.Id,
                Stage = "Intake / Check-in",
                Priority = "Normal",
                LineItems =
                [
                    new SaveEstimateLineItemRequest
                    {
                        LineType = "Labor",
                        Description = "Diagnostics",
                        Quantity = 1m,
                        Rate = 125m,
                        IsTaxable = true
                    },
                    new SaveEstimateLineItemRequest
                    {
                        LineType = "Parts",
                        Description = "Fender",
                        Quantity = 1m,
                        Rate = 99m,
                        IsTaxable = true
                    }
                ]
            },
            null,
            CancellationToken.None);

        var lineItems = await dbContext.EstimateLineItems.IgnoreQueryFilters()
            .Where(x => x.WorkOrderId == workOrderId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        Assert.False(lineItems[0].IsTaxable);
        Assert.True(lineItems[1].IsTaxable);
    }

    [Fact]
    public async Task Save_updates_customer_and_unit_details_from_work_order_editor()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var customer = new Customer { OrganizationId = organizationId, DisplayName = "Ed Blazer", Phone = "555" };
        var unit = new CustomerVehicle { OrganizationId = organizationId, CustomerId = customer.Id, Type = "Street", Year = 2005, Make = "Triumph", Model = "Bonneville" };
        dbContext.Customers.Add(customer);
        dbContext.CustomerVehicles.Add(unit);
        await dbContext.SaveChangesAsync();

        var service = new WorkOrderService(dbContext, new SystemClock());
        await service.SaveAsync(
            organizationId,
            Guid.NewGuid(),
            new SaveWorkOrderRequest
            {
                CustomerId = customer.Id,
                CustomerVehicleId = unit.Id,
                CustomerDisplayName = "Edward Blazer",
                CustomerPhone = "9155551212",
                CustomerEmail = "ed@example.com",
                CustomerPreferredContactMethod = "Text",
                CustomerSmsConsent = true,
                CustomerAddressLine1 = "100 Main",
                CustomerCity = "El Paso",
                CustomerRegion = "TX",
                CustomerPostalCode = "79901",
                UnitType = "Street",
                UnitYear = 2006,
                UnitMake = "Triumph",
                UnitModel = "Bonneville T100",
                UnitVin = "VIN123",
                UnitMileageIn = 1234m,
                Stage = "Intake / Check-in",
                Priority = "Normal"
            },
            null,
            CancellationToken.None);

        Assert.Equal("Edward Blazer", customer.DisplayName);
        Assert.Equal("9155551212", customer.MobilePhone);
        Assert.True(customer.AllowSmsMarketing);
        Assert.Equal("Bonneville T100", unit.Model);
        Assert.Equal(1234m, unit.MileageIn);
        Assert.Equal("79901", (await dbContext.CustomerAddresses.IgnoreQueryFilters().SingleAsync(x => x.CustomerId == customer.Id)).PostalCode);
    }

    [Fact]
    public async Task Intake_creates_customer_vehicle_and_work_order_for_company()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var advisor = new Employee { OrganizationId = organizationId, DisplayName = "Bradley Molen", Status = "Active", IsServiceAdvisor = true };
        dbContext.Employees.Add(advisor);
        dbContext.GlobalVehicles.Add(new GlobalVehicle { Year = 2005, Make = "Triumph", Model = "Bonneville T100 800" });
        await dbContext.SaveChangesAsync();

        var service = new WorkOrderService(dbContext, new SystemClock());
        var result = await service.CreateFromIntakeAsync(
            organizationId,
            Guid.NewGuid(),
            new CreateWorkOrderIntakeRequest
            {
                FirstName = "Ed",
                LastName = "Blazer",
                Phone = "+191512338927",
                Email = "ed@example.com",
                PreferredContactMethod = "Phone",
                PostalCode = "79901",
                City = "El Paso",
                Region = "TX",
                SmsConsent = true,
                ServiceAdvisorEmployeeId = advisor.Id,
                Type = "Street",
                Year = 2005,
                Make = "Triumph",
                Model = "Bonneville T100 800",
                RequestedService = "Carburetors need rebuilt"
            },
            null,
            CancellationToken.None);

        var customer = await dbContext.Customers.IgnoreQueryFilters().SingleAsync(x => x.Id == result.CustomerId);
        var vehicle = await dbContext.CustomerVehicles.IgnoreQueryFilters().SingleAsync(x => x.Id == result.CustomerVehicleId);
        var workOrder = await dbContext.WorkOrders.IgnoreQueryFilters().SingleAsync(x => x.Id == result.WorkOrderId);

        Assert.Equal(organizationId, customer.OrganizationId);
        Assert.Equal("Ed Blazer", customer.DisplayName);
        Assert.True(customer.AllowSmsMarketing);
        Assert.Equal("79901", (await dbContext.CustomerAddresses.IgnoreQueryFilters().SingleAsync(x => x.CustomerId == customer.Id)).PostalCode);
        Assert.Equal(customer.Id, vehicle.CustomerId);
        Assert.Equal("Triumph", vehicle.Make);
        Assert.Equal(vehicle.Id, workOrder.CustomerVehicleId);
        Assert.Equal("Intake / Check-in", workOrder.Stage);
        Assert.Equal("Carburetors need rebuilt", workOrder.RequestedService);
    }

    [Fact]
    public async Task Intake_pin_verification_returns_active_service_advisor()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var employee = new Employee
        {
            OrganizationId = organizationId,
            DisplayName = "Bradley Molen",
            Status = "Active",
            IsServiceAdvisor = true
        };
        var user = new ApplicationUser
        {
            OrganizationId = organizationId,
            EmployeeId = employee.Id,
            Employee = employee,
            Email = "advisor@example.com",
            NormalizedEmail = "ADVISOR@EXAMPLE.COM",
            DisplayName = "Bradley Molen",
            PasswordHash = "hash",
            PinHash = HashPin(organizationId, "1234")
        };
        dbContext.Employees.Add(employee);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new WorkOrderService(dbContext, new SystemClock());

        var result = await service.VerifyIntakePinAsync(organizationId, "1234", CancellationToken.None);
        var wrongPin = await service.VerifyIntakePinAsync(organizationId, "9999", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(employee.Id, result.EmployeeId);
        Assert.Equal("Bradley Molen", result.DisplayName);
        Assert.Null(wrongPin);
    }

    [Fact]
    public async Task Intake_requires_ymm_unless_vehicle_is_bypassed()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var service = new WorkOrderService(dbContext, new SystemClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFromIntakeAsync(
            organizationId,
            Guid.NewGuid(),
            new CreateWorkOrderIntakeRequest
            {
                FirstName = "Ed",
                LastName = "Blazer",
                Phone = "91512338927",
                SmsConsent = true,
                RequestedService = "Check-in"
            },
            null,
            CancellationToken.None));

        var result = await service.CreateFromIntakeAsync(
            organizationId,
            Guid.NewGuid(),
            new CreateWorkOrderIntakeRequest
            {
                FirstName = "Ed",
                LastName = "Blazer",
                Phone = "91512338927",
                SmsConsent = true,
                BypassVehicleSelection = true,
                RequestedService = "Check-in"
            },
            null,
            CancellationToken.None);

        var workOrder = await dbContext.WorkOrders.IgnoreQueryFilters().SingleAsync(x => x.Id == result.WorkOrderId);
        Assert.Null(workOrder.CustomerVehicleId);
    }

    [Fact]
    public async Task Intake_requires_sms_consent()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var service = new WorkOrderService(dbContext, new SystemClock());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFromIntakeAsync(
            organizationId,
            Guid.NewGuid(),
            new CreateWorkOrderIntakeRequest
            {
                FirstName = "Ed",
                LastName = "Blazer",
                Phone = "91512338927",
                BypassVehicleSelection = true,
                RequestedService = "Check-in"
            },
            null,
            CancellationToken.None));

        Assert.Equal("SMS consent is required.", exception.Message);
    }

    [Fact]
    public async Task Intake_rejects_customer_and_vehicle_from_other_company()
    {
        await using var dbContext = CreateContext();
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var customerB = new Customer { OrganizationId = organizationB, DisplayName = "Other Customer" };
        var vehicleB = new CustomerVehicle { OrganizationId = organizationB, CustomerId = customerB.Id, Type = "Street", Year = 2005, Make = "Triumph", Model = "Bonneville" };
        var customerA = new Customer { OrganizationId = organizationA, DisplayName = "A Customer", FirstName = "A", LastName = "Customer", Phone = "555" };
        dbContext.Customers.AddRange(customerA, customerB);
        dbContext.CustomerVehicles.Add(vehicleB);
        await dbContext.SaveChangesAsync();

        var service = new WorkOrderService(dbContext, new SystemClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFromIntakeAsync(
            organizationA,
            Guid.NewGuid(),
            new CreateWorkOrderIntakeRequest
            {
                CustomerId = customerB.Id,
                FirstName = "Other",
                LastName = "Customer",
                Phone = "555",
                SmsConsent = true,
                BypassVehicleSelection = true,
                RequestedService = "Check-in"
            },
            null,
            CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFromIntakeAsync(
            organizationA,
            Guid.NewGuid(),
            new CreateWorkOrderIntakeRequest
            {
                CustomerId = customerA.Id,
                CustomerVehicleId = vehicleB.Id,
                FirstName = "A",
                LastName = "Customer",
                Phone = "555",
                SmsConsent = true,
                Type = "Street",
                Year = 2005,
                Make = "Triumph",
                Model = "Bonneville",
                RequestedService = "Check-in"
            },
            null,
            CancellationToken.None));
    }

    [Fact]
    public async Task AddAttachments_stores_work_order_media_for_company()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var customer = new Customer { OrganizationId = organizationId, DisplayName = "Ed Blazer" };
        var workOrder = new WorkOrder
        {
            OrganizationId = organizationId,
            WorkOrderNumber = "WO-000001",
            CustomerId = customer.Id,
            Stage = "Intake / Check-in",
            Priority = "Normal",
            OpenedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.Customers.Add(customer);
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();

        var service = new WorkOrderService(dbContext, new SystemClock());
        await service.AddAttachmentsAsync(
            organizationId,
            Guid.NewGuid(),
            new AddWorkOrderAttachmentRequest
            {
                WorkOrderId = workOrder.Id,
                Attachments =
                [
                    new WorkOrderAttachmentUpload("Photo", "front.jpg", "/uploads/front.jpg", "image/jpeg")
                ]
            },
            null,
            CancellationToken.None);

        var attachment = await dbContext.WorkOrderAttachments.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(organizationId, attachment.OrganizationId);
        Assert.Equal(workOrder.Id, attachment.WorkOrderId);
        Assert.Equal("Photo", attachment.AttachmentType);
    }

    private static ApplicationDbContext CreateContext()
    {
        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            currentUser,
            new SystemClock(),
            new TenantProvider(currentUser));
    }

    private static string HashPin(Guid organizationId, string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{organizationId:N}:{pin}"));
        return Convert.ToHexString(bytes);
    }
}

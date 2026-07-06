using iM1os.Application.BusinessAdministration;
using iM1os.Application.Employees;

namespace iM1os.Web.Models;

public sealed record HrWorkspacePage(
    BusinessAdministrationWorkspace Administration,
    EmployeesWorkspace Employees,
    string Title,
    string Description);

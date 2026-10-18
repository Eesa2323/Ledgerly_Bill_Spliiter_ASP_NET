# Bill Splitter

A Splitwise-inspired expense-sharing application built for precise money handling, currency conversion, secure authentication, and practical SQL Server data modeling.

The application includes:

- ASP.NET Core Web API
- Responsive Ledgerly browser dashboard served by the same ASP.NET process
- SQL Server persistence through Entity Framework Core
- ASP.NET Core Identity with JWT access tokens and rotating refresh tokens
- Username-based user discovery
- Group and direct expenses
- Expense invitations and settlement tracking
- Budgets and live currency conversion

## Technology

- .NET 10 and ASP.NET Core Web API
- Entity Framework Core with SQL Server
- ASP.NET Core Identity
- JWT bearer authentication
- FluentValidation
- SQL Server LocalDB for local development
- Frankfurter exchange-rate API
- HTML, CSS, and browser JavaScript served from `wwwroot`

## Application functionality

### Accounts and usernames

Registration requires a unique username, email address, and password. Usernames are 3-30 characters and may contain only letters, numbers, and underscores. User search uses usernames rather than database IDs. Existing email-based accounts can still use their email as a login fallback.

### Groups

Users can create groups from the dashboard, search users by username, add multiple users directly to a group, view only groups where they are members, and add registered usernames to existing groups. Group membership changes do not create expense invitations.

### Expenses

The Add expense modal has two modes.

#### Group expense

1. Select an existing group from the user's groups.
2. Group members are loaded automatically.
3. Enter the expense and edit individual shares if needed.
4. The expense is saved immediately to the group.

Group expenses do not create pending invitations because the participants already belong to the group.

#### Direct expense

1. Select Direct expense.
2. Search for exactly one username.
3. Enter the total and the selected user's share.
4. The expense is saved without a group.
5. The selected user receives a pending expense invitation.

Users can filter expenses by group or view direct expenses separately. Expense records show the payer, the current user's share, and settlement status. Participants can settle their own share from the dashboard.

### Invitations

The notifications tab displays pending direct expense invitations. Recipients can accept or dismiss invitations and view the payer, description, currency, and their share.

### Budgets and currency

Users can create budgets for groups with a currency, amount, and date range. Currency conversion uses the Frankfurter API and rounds displayed converted amounts to two decimal places using decimal arithmetic.

## Money and validation rules

Money values use C# `decimal`, never `float` or `double`. SQL Server stores financial values as `decimal(19,4)`.

FluentValidation and controller checks enforce non-empty descriptions and names, positive amounts, four-decimal precision limits, three-letter uppercase currency codes, exact group split totals, valid direct recipient shares, valid usernames, and valid budget date ranges. The browser performs early validation, but the API remains authoritative.

## Security model

- Passwords are handled by ASP.NET Core Identity.
- Access tokens are short-lived JWTs.
- Refresh tokens are randomly generated, hashed before storage, and rotated when used.
- Protected endpoints require `Authorization: Bearer <access-token>`.
- Users can view only their own groups, expenses, notifications, and settlement records.
- User search returns usernames and display names only; password and token fields are never exposed.
- Production JWT keys and connection strings must not be committed to source control.

## Run locally

The API and client run together. There is no separate frontend server.

### Prerequisites

- .NET 10 SDK
- SQL Server LocalDB with the `MSSQLLocalDB` instance

The default connection string is:

```text
Server=(localdb)\MSSQLLocalDB;Database=BillSplitterDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

Full SQL Server or SQL Server Express can be used by replacing the connection string with the correct instance.

### Start the application

From the project directory:

```powershell
cd C:\Users\Eesa\Downloads\bill-splitter-asp
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "a-long-random-development-secret-at-least-32-characters"
dotnet run
```

Alternatively:

```powershell
.\run-local.ps1
```

Open the dashboard at `http://localhost:5080/`.

Open the development OpenAPI document at `http://localhost:5080/openapi/v1.json`.

The application applies pending EF migrations during startup. To apply them manually:

```powershell
dotnet ef database update
```

## API endpoints

### Authentication

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`

### Users and groups

- `GET /api/users/search?query=username`
- `GET /api/groups`
- `POST /api/groups`
- `POST /api/groups/{groupId}/members`

### Group expenses

- `GET /api/groups/{groupId}/expenses`
- `POST /api/groups/{groupId}/expenses`
- `POST /api/groups/{groupId}/expenses/{expenseId}/settle`

### Direct expenses

- `GET /api/expenses`
- `POST /api/expenses`
- `POST /api/expenses/{expenseId}/settle`

### Invitations

- `GET /api/notifications`
- `POST /api/notifications/{notificationId}/accept`
- `DELETE /api/notifications/{notificationId}`

### Budgets and currency

- `GET /api/groups/{groupId}/budgets`
- `POST /api/groups/{groupId}/budgets`
- `GET /api/currency/convert?amount=10&from=GBP&to=EUR`

## Project structure

- `Program.cs`: dependency injection, SQL Server, Identity, JWT, static files, migrations, and startup schema repair
- `Domain.cs`: users, groups, expenses, splits, invitations, budgets, and refresh tokens
- `AppDbContext.cs`: EF Core relationships, indexes, and money precision
- `Contracts.cs`: request and response records
- `Validation.cs`: FluentValidation rules
- `Controllers/`: API endpoints for auth, users, groups, expenses, direct expenses, notifications, budgets, and currency
- `Migrations/`: SQL Server schema migrations
- `wwwroot/index.html`: dashboard structure and modal forms
- `wwwroot/app.js`: browser state and API integration
- `wwwroot/styles.css`: responsive dashboard styling
- `run-local.ps1`: local development launcher

## Troubleshooting

If port `5080` is already in use, stop the old application instance before starting another:

```cmd
taskkill /IM bill-splitter-asp.exe /F
dotnet run
```

Or open the existing server at `http://localhost:5080/` instead of starting a second instance.

If a direct expense reports that `GroupId` cannot be `NULL`, stop and restart the application so the direct-expense migration and startup schema repair can run. The application uses SQL Server LocalDB by default; verify that the `MSSQLLocalDB` instance exists.

If PowerShell blocks the launcher:

```powershell
powershell -ExecutionPolicy Bypass -File .\run-local.ps1
```

The `NU1902` and `NU1903` messages are dependency vulnerability warnings from restore. They are separate from application startup errors and should be addressed by updating the affected dependency chain before production deployment.

Do not commit JWT secrets, passwords, or production connection strings.

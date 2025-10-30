# GrapheneTrace

A MedTech application for continuous pressure ulcer prevention using e-textile pressure mapping sensors.

## Project Structure

### CLI-demo/
**REFERENCE ONLY - NOT AUTHORITATIVE**

The `CLI-demo` folder contains a quick proof-of-concept console application that was built to validate core concepts. While it may contain some useful implementation ideas, it should **NOT** be considered authoritative or production-ready.

**Do not use CLI-demo as:**
- A source of truth for architecture decisions
- Production-quality code to copy directly
- The definitive implementation approach

**You may reference CLI-demo for:**
- Understanding initial domain model concepts
- Seeing one possible approach to pressure map processing
- Getting ideas for algorithms (but verify and improve them)

### web-implementation/
**ACTIVE DEVELOPMENT - PRIMARY FOCUS**

All new development should happen in the `web-implementation` folder. This is a Blazor Web App that will become the production application.

### Requirements/
Business requirements, user stories, and client requests that apply to all implementations.

---

## Blazor Project Structure Guide

For contributors unfamiliar with Blazor or C#, here's what you need to know:

### What is Blazor?

Blazor is a modern web framework for building interactive client-side web UIs using C# instead of JavaScript. It runs on .NET and can execute either on the server (Blazor Server) or in the browser via WebAssembly (Blazor WebAssembly).

**This project uses Blazor Server**, which means:
- C# code runs on the server
- UI updates are sent to the browser via SignalR (WebSocket connection)
- Low bandwidth requirements
- No client-side compilation needed

### Project Layout

```
web-implementation/
├── Components/              # Reusable UI components and pages
│   ├── Layout/             # Layout components (nav, header, footer)
│   ├── Pages/              # Routable pages (like /counter, /weather)
│   └── _Imports.razor      # Global using statements for components
├── wwwroot/                # Static files (served directly to browser)
│   ├── css/                # Stylesheets
│   ├── js/                 # JavaScript files (if needed)
│   └── favicon.ico         # Site icon
├── Properties/             # IDE and deployment settings
│   └── launchSettings.json # Development server configuration
├── appsettings.json        # Application configuration (DB connection, etc.)
├── Program.cs              # Application entry point and service configuration
└── GrapheneTrace.Web.csproj # Project file (like package.json for npm)
```

### Key File Types

#### `.razor` Files
Blazor components that mix HTML and C# code:

```razor
@page "/counter"

<h1>Counter</h1>
<p>Current count: @currentCount</p>
<button @onclick="IncrementCount">Click me</button>

@code {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
```

- `@page "/route"` - Makes component accessible at URL
- `@code { }` - C# code block for logic
- `@variableName` - Display C# variable in HTML
- `@onclick` - Event handlers

#### `.cs` Files
Pure C# classes for business logic, models, and services:

```csharp
public class PressureMap
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public byte[,] Data { get; set; }
}
```

#### `.csproj` File
Project configuration (like `package.json`):
- Lists NuGet package dependencies
- Specifies target framework (e.g., .NET 8.0)
- Defines build settings

### C# Basics for New Contributors

#### Variables and Types
```csharp
// C# is statically typed
int count = 5;
string name = "GrapheneTrace";
bool isActive = true;
DateTime now = DateTime.UtcNow;

// Type inference with 'var'
var message = "This is a string"; // Compiler knows it's string
```

#### Properties (not the same as JavaScript properties!)
```csharp
public class User
{
    // Auto-property (generates backing field automatically)
    public string Name { get; set; }

    // Read-only property
    public string Email { get; private set; }

    // Computed property
    public string FullInfo => $"{Name} ({Email})";
}
```

#### Nullable Types
```csharp
// C# 8.0+ has nullable reference types
string? optionalName = null;  // Can be null
string requiredName = "Bob";   // Cannot be null

int? optionalNumber = null;    // Nullable value type
int requiredNumber = 42;       // Not nullable
```

#### LINQ (Language Integrated Query)
```csharp
var numbers = new List<int> { 1, 2, 3, 4, 5 };

// Filter
var evenNumbers = numbers.Where(n => n % 2 == 0);

// Transform
var doubled = numbers.Select(n => n * 2);

// Aggregate
var sum = numbers.Sum();
var max = numbers.Max();
```

#### Async/Await
```csharp
// Async method must return Task or Task<T>
public async Task<List<User>> GetUsersAsync()
{
    // await pauses execution until result is ready
    var users = await _database.Users.ToListAsync();
    return users;
}

// Calling async method
var users = await GetUsersAsync();
```

### Common Commands

```bash
# Restore dependencies (like npm install)
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run

# Run in watch mode (auto-rebuild on changes)
dotnet watch run

# Add a NuGet package
dotnet add package PackageName

# Create a new Blazor component
dotnet new razorcomponent -n ComponentName -o Components
```

### Development Workflow

1. **Make changes** to `.razor` or `.cs` files
2. **Run with watch mode**: `dotnet watch run`
3. **Browser auto-refreshes** on file save
4. **Check console** for build errors or warnings
5. **Test in browser** at `https://localhost:5001`

### Dependency Injection

Blazor uses dependency injection heavily:

```csharp
// Register service in Program.cs
builder.Services.AddScoped<IUserService, UserService>();

// Inject into component
@inject IUserService UserService

@code {
    protected override async Task OnInitializedAsync()
    {
        var users = await UserService.GetAllAsync();
    }
}
```

### Helpful Resources

- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- [C# Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/)
- [.NET CLI Reference](https://learn.microsoft.com/en-us/dotnet/core/tools/)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)

---

## Collaboration Workflow

We use a feature branch workflow to keep development organized and make code reviews easier. Here's the process:

### The Basic Flow

```bash
# 1. Start a new feature - create a branch from main
git checkout main
git pull origin main
git checkout -b feature/user-authentication

# 2. Work on your feature
# - Make changes to code
# - Test your work locally
# - Commit regularly with clear messages

git add .
git commit -m "Add login form component"

# Keep making commits as you build the feature
git commit -m "Add password validation"
git commit -m "Connect login to backend API"

# 3. Push your branch to GitHub
git push -u origin feature/user-authentication

# 4. Open a Pull Request (PR) on GitHub
# - Go to the repository on GitHub
# - Click "Compare & pull request"
# - Write a description of what you built
# - Request a review from a teammate

# 5. Wait for review and approval
# - Address any feedback
# - Push additional commits if changes are needed

# 6. Someone else merges your PR
# - DO NOT merge your own PRs
# - Once approved, a teammate will merge it

# 7. Start your next feature with a fresh branch
git checkout main
git pull origin main
git checkout -b feature/dashboard-layout
```

### Best Practices

**DO:**
- Create a new branch for each feature/task
- Use descriptive branch names: `feature/login-page`, `fix/database-connection`, `refactor/user-service`
- Make small, focused commits with clear messages
- Push your branch early and often (don't wait until it's "perfect")
- Keep branches short-lived - aim to complete and merge within a few days
- Pull from `main` regularly to stay up-to-date: `git pull origin main`
- Test your code before pushing
- Write clear PR descriptions explaining what you built and why

**DON'T:**
- Work directly on the `main` branch
- Keep a branch open for weeks - break large features into smaller pieces
- Merge your own PRs - always get a review
- Force push (`git push -f`) unless you're absolutely sure what you're doing
- Commit broken code that won't build
- Leave vague commit messages like "fixed stuff" or "updates"

### Example Commit Messages

```bash
# Good - clear and descriptive
git commit -m "Add patient profile page with pressure map history"
git commit -m "Fix null reference error in alert service"
git commit -m "Refactor database context to use fluent API"

# Bad - too vague
git commit -m "updates"
git commit -m "fixed it"
git commit -m "changes"
```

### Handling Merge Conflicts

If someone else merged changes that conflict with yours:

```bash
# Pull the latest main branch
git checkout main
git pull origin main

# Go back to your feature branch and merge main into it
git checkout feature/your-feature
git merge main

# Git will tell you about conflicts
# Open the conflicting files and look for conflict markers:
# <<<<<<< HEAD
# Your changes
# =======
# Their changes
# >>>>>>> main

# Edit the files to resolve conflicts
# Remove the conflict markers and keep the correct code

# After fixing conflicts
git add .
git commit -m "Resolve merge conflicts with main"
git push
```

### Why This Workflow?

This approach might feel like extra steps at first, but it helps us:
- **Review each other's code** before it goes into main - catch bugs early
- **Keep main stable** - it should always build and run
- **Work in parallel** - multiple people can work on different features without stepping on each other's toes
- **Track progress** - PRs show what everyone is working on
- **Learn from each other** - code reviews are great learning opportunities

### Quick Reference

| Task | Command |
|------|---------|
| Create a new branch | `git checkout -b feature/name` |
| Switch branches | `git checkout branch-name` |
| See what you changed | `git status` and `git diff` |
| Commit your changes | `git add .` then `git commit -m "message"` |
| Push to GitHub | `git push` (or `git push -u origin branch-name` first time) |
| Update from main | `git checkout main && git pull origin main` |
| Delete old branch | `git branch -d feature/old-feature` |

---

## Getting Started

### Prerequisites

- **Docker** and **Docker Compose** installed
- **.NET 9.0 SDK** installed ([Download here](https://dotnet.microsoft.com/download/dotnet/9.0))
- **VPN Disabled** - If you use a VPN, disable it before running the application as it can interfere with port mappings and prevent the database connection from working properly

### Setup Instructions

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd GrapheneTrace
   ```

2. **Start the PostgreSQL database container**
   ```bash
   docker-compose up -d postgres
   ```

   Wait a few seconds for the database to initialize. You can check the logs with:
   ```bash
   docker logs graphenetrace_postgres
   ```

3. **Navigate to the web implementation directory**
   ```bash
   cd web-implementation
   ```

4. **Install dependencies**
   ```bash
   dotnet restore
   ```

5. **Apply database migrations**

   This creates the database schema (tables, indexes, etc.):
   ```bash
   dotnet ef database update
   ```

   You should see output confirming that migrations were applied successfully.

6. **Run the application**
   ```bash
   dotnet watch run
   ```

   Or for production mode without hot reload:
   ```bash
   dotnet run
   ```

7. **Open your browser to** `https://localhost:5001`

   The application should now be running and connected to the database.

### Important Notes

- **VPN Warning**: Make sure any VPN is disabled before starting the application. VPNs can mess with port mappings and prevent the application from connecting to the database on `localhost:5432`.
- **First-time setup**: You only need to run `docker-compose up` and `dotnet ef database update` once. After that, just run `dotnet watch run` to start the application.
- **Stopping the database**: Use `docker-compose down` to stop the database container.
- **Resetting the database**: Use `docker-compose down -v` to stop and wipe all data for a fresh start.

### Check Your Assigned User Stories

- See `Requirements/UserStories.md` for your story assignments
- Reference story numbers in your commits and PRs (e.g., "Implements Story #1")
- Check off stories as you complete them

---

## Database Setup and Interaction

GrapheneTrace uses PostgreSQL for data storage, running in a Docker container. The database schema is managed through **Entity Framework Core migrations** (not SQL scripts).

### Quick Start

```bash
# Start the database
docker-compose up -d postgres

# Stop the database
docker-compose down

# Stop and wipe all data (fresh start)
docker-compose down -v
```

### Connection Details

| Property | Value |
|----------|-------|
| Host | `localhost` (127.0.0.1) |
| Port | `5432` |
| Database | `graphenetrace` |
| Username | `graphene_user` |
| Password | `changeme` |

**Connection String:**
```
Host=127.0.0.1;Port=5432;Database=graphenetrace;Username=graphene_user;Password=changeme
```

### Running Migrations

The database schema is created and updated using EF Core migrations:

```bash
# Navigate to web project
cd web-implementation

# Apply all pending migrations (run this after starting the database)
dotnet ef database update

# Create a new migration (after changing models)
dotnet ef migrations add MigrationName

# Remove the last migration (if not yet applied)
dotnet ef migrations remove
```

**Important:** The first time you set up the database, you MUST run `dotnet ef database update` to create the tables.

### Connecting with psql

#### From Inside the Docker Container
```bash
# Interactive shell
docker exec -it graphenetrace_postgres psql -U graphene_user -d graphenetrace

# Run a single query
docker exec graphenetrace_postgres psql -U graphene_user -d graphenetrace -c "SELECT * FROM \"AspNetUsers\";"
```

#### From Your Host Machine
```bash
# Interactive shell (password: changeme)
PGPASSWORD=changeme psql -h 127.0.0.1 -p 5432 -U graphene_user -d graphenetrace

# Run a single query
PGPASSWORD=changeme psql -h 127.0.0.1 -p 5432 -U graphene_user -d graphenetrace -c "\dt"
```

### Useful psql Commands

Once connected to the database:

```sql
\dt                    -- List all tables
\d "TableName"         -- Describe a table structure
\l                     -- List all databases
\du                    -- List all users
\q                     -- Quit psql

-- View users
SELECT * FROM "AspNetUsers";

-- Check migrations history
SELECT * FROM "__EFMigrationsHistory";
```

### Database Tables

After running migrations, you'll have these tables:

- **AspNetUsers** - User accounts (with custom fields: FirstName, LastName, UserType, DeactivatedAt)
- **AspNetRoles** - Role definitions
- **AspNetUserRoles** - User-role mappings
- **AspNetUserClaims** - Custom user claims
- **AspNetUserLogins** - External login providers
- **AspNetUserTokens** - Password reset/2FA tokens
- **AspNetRoleClaims** - Role-based claims
- **__EFMigrationsHistory** - Tracks which migrations have been applied

### Troubleshooting

**"Connection refused" error?**
- Make sure the database container is running: `docker ps | grep postgres`
- Wait a few seconds after `docker-compose up` for the database to initialize
- Check container health: `docker logs graphenetrace_postgres`

**"No tables found"?**
- You need to run migrations: `cd web-implementation && dotnet ef database update`

**"Password authentication failed"?**
- Make sure you're using password `changeme`
- Check your connection string in `appsettings.Development.json`

**Need to reset the database?**
```bash
# Stop container and delete all data
docker-compose down -v

# Start fresh container
docker-compose up -d postgres

# Wait a few seconds, then run migrations
cd web-implementation
dotnet ef database update
```

---

## Tracking Progress

All user stories are tracked in `Requirements/UserStories.md` with:
- Story assignments by developer
- Checkboxes to track completion
- Progress summary table
- Category groupings (Login, Account Management, Pressure Data, etc.)

**When working on a story:**
1. Reference the story number in your branch name: `feature/story-1-login`
2. Mention it in PR descriptions: "Implements Story #1: User login"
3. Check off the checkbox when the story is fully implemented and merged

For full project requirements and architecture, see the `Requirements/` folder.

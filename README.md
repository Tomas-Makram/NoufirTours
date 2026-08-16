# 🚌 NoufirTours

<p align="center">
  <img src="https://img.shields.io/badge/ASP.NET_Core-MVC-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">
  <img src="https://img.shields.io/badge/C%23-.NET_10-239120?style=for-the-badge&logo=csharp&logoColor=white">
  <img src="https://img.shields.io/badge/Entity_Framework-Core-6C3483?style=for-the-badge">
  <img src="https://img.shields.io/badge/SQL_Server-Database-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white">
  <img src="https://img.shields.io/badge/QRCoder-QR_Tickets-2E86C1?style=for-the-badge">
  <img src="https://img.shields.io/badge/ClosedXML-Excel_Export-217346?style=for-the-badge&logo=microsoftexcel&logoColor=white">
  <img src="https://img.shields.io/badge/Cookie-Authentication-E67E22?style=for-the-badge&logo=shield&logoColor=white">
  <img src="https://img.shields.io/badge/HTML%2FCSS%2FJS-Frontend-F39C12?style=for-the-badge&logo=html5&logoColor=white">
</p>

---

## 📋 Overview

**NoufirTours** is a professional web application designed for transport and tourism companies to manage daily trip operations — from trip planning, bus and driver assignment, to seat booking, ticket issuance, and performance monitoring through a full-featured dashboard.

---

## ✨ Key Features

### 🎫 Booking Management
- Seat booking with a visual bus seat-map layout
- Electronic ticket generation with QR codes
- Trip search and instant booking
- Ticket cancellation with deletion history tracking

### 🗺️ Trip Management
- Create and schedule trips with pickup/drop-off stops
- Automated recurring trip planning (Auto Trip Planner)
- Assign buses and drivers to each trip

### 🚍 Fleet Management
- Manage buses and seat configurations
- Manage drivers and their contact information

### 📊 Dashboard
- Comprehensive statistics and reports on bookings and trips
- Filter and view data by time period
- Export reports to Excel files

### 🔐 Security & Authentication
- Secure login system built on Cookie Authentication
- Data encryption and hashing for sensitive information
- Role-based access control (Admin / Staff)
- Session management with automatic timeout
- Security Headers Middleware for common attack prevention

### 🎨 User Interface
- Dark and Light theme support
- Responsive design for all devices

---

## 🛠️ Tech Stack

| Technology | Description |
|---|---|
| **ASP.NET Core MVC** | Main framework (.NET 10) |
| **C#** | Backend development |
| **Entity Framework Core** | Database access (ORM) |
| **SQL Server** | Primary database |
| **Cookie Authentication** | Authentication system |
| **QRCoder** | QR code generation for tickets |
| **ClosedXML** | Excel report exports |
| **HTML / CSS / JS** | Frontend |

---

## 📁 Project Structure

```
NoufirTours/
├── Controllers/          # Auth, Booking, Dashboard, Home, Trips
├── Models/               # Data and view models
│   ├── Auth/             # Login models
│   ├── Bookings/         # Booking and ticket models
│   ├── Dashboard/        # Dashboard view models
│   ├── Home/             # Home page models
│   └── Trips/            # Trip models (Accounts, Buses, Drivers)
├── Views/                # Razor Views
├── Services/             # Encryption, auto-planning, location, security
├── Data/                 # Database entities and DbContext
├── Migrations/           # EF Core migrations
├── Attribute/            # Custom attributes
├── wwwroot/              # Static files (CSS, JS, images)
└── Program.cs            # Application entry point and service configuration
```

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)

### Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/Tomas-Makram/NoufirTours.git
   cd NoufirTours/NoufirTours
   ```

2. **Configure the database**

   Create an `appsettings.json` file and add your connection string:
   ```json
   {
     "ConnectionStrings": {
       "Connection": "Server=YOUR_SERVER;Database=NoufirToursDB;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

3. **Apply migrations**
   ```bash
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

---

## ⚠️ Security Notice

- `appsettings.json`, `appsettings.Development.json`, and `data-protection-keys/` are **excluded** from this repository as they contain sensitive data.
- You must create these files manually when setting up the project locally.
- Never commit API keys, passwords, or connection strings to the repository.

---

## 👤 Author

**Tomas Makram**

- GitHub: [@Tomas-Makram](https://github.com/Tomas-Makram)

---

## ⭐ Support

If you like this project, give it a ⭐ on GitHub!

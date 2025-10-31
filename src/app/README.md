# Customer Demo App

A simplified Blazor WebAssembly SPA that demonstrates personalized customer recommendations powered by Microsoft Fabric and Cosmos DB.

## Features

- **Customer Selection**: Browse random customers from your Cosmos DB
- **Customer Profiles**: View detailed customer information including loyalty points and preferences
- **Personalized Recommendations**: Display ML-generated recommendations from the Microsoft Fabric pipeline
- **Responsive Design**: Modern Bootstrap UI that works on all devices

## Prerequisites

- .NET 8.0 SDK
- Azure Cosmos DB account with customer data
- Customer data populated using the lab exercises

## Setup Instructions

### Important: Microsoft Fabric Cosmos DB Considerations

This application has been optimized for **Microsoft Fabric Cosmos DB**, which has some differences from regular Azure Cosmos DB:

#### Key Differences:
- ✅ **Connection Mode**: Uses Gateway mode (required for Fabric)
- ✅ **Authentication**: Azure CLI credentials supported in Blazor Server
- ⚠️ **Limitations**: Some SDK operations are not supported in Fabric
- 🔧 **Error Handling**: Enhanced error messages for Fabric-specific issues

#### Common Fabric Error:
If you see: `"Operation Read on resource Address is not supported for Azure Cosmos DB database in Microsoft Fabric"`
- This is a **Fabric limitation**, not a configuration issue
- The app automatically handles this with appropriate error messaging
- Consider using local JSON fallback for development if needed

### Data Source Options

The application supports two data sources and will automatically choose the appropriate one:

#### 1. Microsoft Fabric Cosmos DB (Primary)
When a valid Fabric Cosmos DB endpoint is provided, the application connects using Azure CLI credentials.

#### 2. Local JSON File (Fallback)
When no valid endpoint is configured, the application automatically falls back to using a local JSON file (`wwwroot/data/customers.json`) containing sample customer data.

### Enhanced Logging

The application provides detailed logging to help developers understand the data source being used:

**Console Output Examples:**

**Fallback Mode (Default):**
```
=== Customer Data Service Configuration ===
🔄 FALLBACK MODE: Using local JSON file for customer data
   Reason: Cosmos DB connection string not configured
   Data source: /wwwroot/data/customers.json
   **To use Cosmos DB: Update connection string in Program.cs

**Testing the fail-fast behavior:**

To test what happens when Cosmos DB connection fails, temporarily change the connection string in Program.cs to an invalid value:

```csharp
var cosmosConnectionString = "AccountEndpoint=https://invalid.documents.azure.com:443/;AccountKey=invalid;";
```

The application will fail immediately with detailed error messages, which is the intended behavior.
===========================================

🔄 Loading customer data from local JSON file...
✅ Successfully loaded 500 customers from data/customers.json
```

**Cosmos DB Mode (Success):**
```
🌐 COSMOS DB MODE: Attempting to connect to Cosmos DB
   Database: YourDatabase
   Container: customers
   Connection string: [CONFIGURED]
   Note: App will fail if connection cannot be established
🔄 Initializing Cosmos DB connection...
✅ Cosmos DB client initialized successfully
```

**Cosmos DB Mode (Failure - App will crash):**
```
🌐 COSMOS DB MODE: Attempting to connect to Cosmos DB
   Database: YourDatabase
   Container: customers
   Connection string: [CONFIGURED]
   Note: App will fail if connection cannot be established
🔄 Initializing Cosmos DB connection...
❌ CRITICAL: Failed to initialize Cosmos DB client
   Error: [Detailed error message]
   Database: YourDatabase
   Container: customers
   Please check:
   - Connection string is valid
   - Database and container exist
   - Network connectivity to Cosmos DB
   - Account keys are not expired
```

**Important:** When a real Cosmos DB connection string is provided, the application will **fail immediately** if the connection cannot be established. This is intentional behavior to ensure developers know when their Cosmos DB configuration is incorrect.

**UI Indicator:**
The application header shows a badge indicating the current data source:
- 🟡 **Local JSON File** - Using sample data from local file  
- 🟢 **Azure Cosmos DB** - Connected to live database

### 1. Configure Cosmos DB Connection (Optional)

Update the connection details in `Program.cs`:

```csharp
var cosmosConnectionString = "YOUR_COSMOS_DB_CONNECTION_STRING_HERE";
var databaseName = "YOUR_DATABASE_NAME";
var containerName = "customers";
```

**Note**: The application will use the local JSON fallback if the connection string is:
- Empty or null
- Set to `"YOUR_COSMOS_DB_CONNECTION_STRING_HERE"` (default placeholder)
- Starts with `"YOUR_"` (any placeholder value)

You can find your Cosmos DB connection string in:
- Azure Portal → Your Cosmos DB Account → Keys → Primary Connection String

### 2. Customer Data Format

The app expects customer data in this JSON format:

```json
{
  "id": "customer001",
  "customerId": "C001",
  "name": "John Doe",
  "email": "john@example.com",
  "loyaltyPoints": 1250,
  "lastPurchaseDate": "2024-01-15T10:30:00Z",
  "preferences": {
    "favoriteDrink": "Espresso",
    "airport": "SEA",
    "dietaryRestrictions": ["vegan"],
    "notificationPreferences": {
      "email": true,
      "push": false
    }
  },
  "recommendations": [
    {
      "menuItemId": "M001",
      "name": "Almond Milk Latte",
      "score": 0.95,
      "reason": "Based on your vegan preference and coffee history"
    }
  ],
  "registeredAt": "2023-06-01T09:00:00Z",
  "updatedAt": "2024-01-15T10:30:00Z"
}
```

### 3. Run the Application

```bash
# Navigate to the app directory
cd src/app

# Restore dependencies
dotnet restore

# Run the application
dotnet run
```

The app will start at `https://localhost:5001` (or as displayed in the console).

## Project Structure

```
src/app/
├── Models/
│   └── Customer.cs              # Customer data models
├── Services/
│   └── CosmosDbService.cs       # Cosmos DB data access
├── Pages/
│   └── Home.razor               # Main customer display page
├── Shared/
│   └── MainLayout.razor         # Application layout
├── wwwroot/
│   ├── index.html               # Entry point
│   ├── app.css                  # Custom styles
│   └── appsettings.json         # Configuration template
├── App.razor                    # Root component
├── Program.cs                   # Application startup
└── _Imports.razor               # Global using statements
```

## Customization

### Styling
- Modify `wwwroot/app.css` for custom styles
- The app uses Bootstrap 5.3 for responsive design
- CSS custom properties are used for consistent theming

### Data Display
- Update `Pages/Home.razor` to modify the customer information layout
- Add new customer properties by updating the `Customer` model
- Customize recommendation display format

### Cosmos DB Queries
- Modify `CosmosDbService.cs` to change how customers are retrieved
- Add filtering or sorting capabilities
- Implement pagination for large datasets

## Troubleshooting

### Common Issues

1. **Connection Errors**
   - Verify your Cosmos DB connection string is correct
   - Ensure your Cosmos DB account allows connections from your location
   - Check that the database and container names match your setup

2. **No Data Displayed**
   - Confirm customer data exists in your container
   - Verify the JSON structure matches the expected format
   - Check browser console for JavaScript errors

3. **Build Errors**
   - Ensure .NET 8.0 SDK is installed
   - Run `dotnet restore` to install dependencies
   - Check that all required NuGet packages are available

### Logs and Debugging

The app logs errors to the browser console. Open Developer Tools (F12) to view detailed error messages.

## Integration with Lab Exercises

This demo app showcases data from:
- **Exercise 1**: Cosmos DB setup and initial data load
- **Exercise 2**: Eventstreams and real-time data processing  
- **Exercise 3**: KQL Database analytics
- **Exercise 4**: Data Warehouse integration
- **Exercise 5**: ML personalization and reverse ETL

The recommendations displayed are generated by the personalization model built in Exercise 5.
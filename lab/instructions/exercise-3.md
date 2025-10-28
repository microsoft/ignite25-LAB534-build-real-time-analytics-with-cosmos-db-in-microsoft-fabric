# Real-Time Streaming of POS Events

In this exercise, you will ingest and query the streaming data and use the Kusto Query Language (KQL) to analyze it.

By the end of this exercise, you'll be able to:

- Store streaming data in an Eventhouse
- Query streaming data using the Kusto Query Language (KQL)
- Build a Silver layer for analytics using KQL functions


## Create an Eventhouse

1. You have already created an Eventstream in the Fabric Environment Setup exercise. You will now create an Eventhouse to ingest and store the streaming data. Navigate to your Fabric workspace and select **+ New item** from the top menu ribbon.

1. In the **New item** pane that opens on the right side, type +++*eventhouse*+++ in the filter text box on the top right of the pane to filter the list of items. Select **Eventhouse (Preview)**.

    ![Screenshot showing how to create a new Eventhouse in Microsoft Fabric](media/create-eventhouse.png)

1. Name the new Eventhouse +++*fc_commerce_eventhouse*+++ and select **Create**.

    ![Screenshot showing the new Eventhouse popup in Microsoft Fabric](media/eventhouse-popup.png)

1. Once the Eventhouse has been created, it will open in a new tab in Fabric.
    ![Screenshot showing the created Eventhouse in Microsoft Fabric](media/eventhouse-created.png)

## Connect your Eventstream output to a KQL Database

1. In Fabric, open your Eventstream.

1. Hover over to the right of the eventstream name and select the **+** icon. It will open a context menu. 

1. Scroll down and select **Eventhouse**.

![Screenshot of adding eventhouse destination to a eventstream](media/add-eventhouse-destination.png)

1. You will now see a new eventhouse node connected to your eventstream. 

1. Select the edit icon (pencil) on the eventhouse node to configure the destination.

![Screenshot of eventhouse card with edit icon highlighted](media/eventhouse-edit-icon.png)

1. Selecting the edit option will open a new pane on the right. Here, you will configure the Eventhouse destination. Enter the following details:


    1. Verify that the data ingestion mode is set to **Event processing before ingestion**.

    1. For the Destination name, enter +++*fc-eventhouse*+++.

    1. Select your workspace, this should be the workspace that your eventstream is in.

    1. Select the Eventhouse you created earlier, +++*fc_commerce_eventhouse*+++.

    1. For the KQL Database, select  +++*fc_commerce_eventhouse*+++.

    1. For the KQL Destination Table, select **Create new** below the empty dropdown and enter the name +++*transactions_live*+++ and select **Done**.
    
    ![Screenshot of creating a new KQL destination table.](media/create-kql-destination-table.png)
    
    1. Verify Input data format is set to **JSON**.

    1. Verify **Activate ingestion after adding the data source** is checked.

    1. Select **Save** to create the Eventhouse destination.

![Screenshot of configuring the eventhouse destination](media/configure-eventhouse-destination.png)

1. From the top menu ribbon select **Publish** to publish the changes to your eventstream.

![Screenshot of publishing eventstream changes](media/publish-eventstream.png)

## Verify live ingestion into Eventhouse and run KQL queries

> [!TIP]
> It may take a few minutes for data to start flowing into the Eventhouse. If you do not see any data when you run the query below, wait a few minutes and try again.

1. Navigate to your Eventhouse tab in Fabric.

![Screenshot of the eventhouse system overview tab](media/eventhouse-tab-overview.png)

1. Select the **fc_commerce_eventhouse** database from the left explorer pane.
1. In the database, select the **transactions_live** table.

![Screenshot of the transactions_live table selected in the eventhouse](media/transactions_live_table.png)

1. In the top ribbon select **Query with code** button and select *Show any 100 records* from the dropdown.
![Screenshot of Query with code in top ribbon of eventhouse](media/query-with-code-eventhouse-button.png)

1. In the new query editor tab, select **Run** to verify that data is being ingested into the Eventhouse in real-time.

![Screenshot of querying with code in eventhouse](media/take-100-query.png)

## Build the Silver Layer for Analytics
1. Replace the existing query in the query editor with the following KQL code to create a Silver layer table that aggregates total sales by menu item:

+++*
.create-or-alter function with (folder="Silver") vw_Pos_Silver() {
    transactions_live
    | where transactionType == "purchase"
    | summarize arg_max(timestamp, *) by transactionId
    | project
        TransactionId  = tostring(transactionId),
        EventTimestamp = todatetime(timestamp),
        CustomerId     = tostring(customerId),
        ShopId         = tostring(shopId),
        AirportId      = tostring(airportId),
        PaymentMethod  = tostring(paymentMethod),
        TotalAmount    = todouble(totalAmount),
        LoyaltyPointsEarned   = toint(coalesce(loyaltyPointsEarned, 0)),
        LoyaltyPointsRedeemed = toint(coalesce(loyaltyPointsRedeemed, 0)),
        DateKey        = toint(format_datetime(todatetime(timestamp), "yyyyMMdd")),
        TimeKey        = toint(format_datetime(todatetime(timestamp), "HHmmss")),
        SourceSystem   = "CosmosDB",
        CreatedAt      = todatetime(timestamp)
}

.create-or-alter function with (folder="Silver") vw_Pos_LineItems() {
    let base =
        transactions_live
        | extend EventTs = coalesce(
            todatetime(column_ifexists("timestamp", datetime(null))),
            todatetime(column_ifexists("EventEnqueuedUtcTime", datetime(null))),
            todatetime(column_ifexists("EventProcessedUtcTime", datetime(null))),
            ingestion_time()
          )
        | where tostring(column_ifexists("transactionType","")) == "purchase"
        | extend items = column_ifexists("items", dynamic(null))
        | where isnotnull(items)
        | mv-expand items;
    base
    | extend
        TransactionId  = tostring(column_ifexists("transactionId","")),
        EventTimestamp = EventTs,
        CustomerId     = tostring(column_ifexists("customerId","")),
        ShopId         = tostring(column_ifexists("shopId","")),
        AirportId      = tostring(column_ifexists("airportId","")),
        PaymentMethod  = tostring(column_ifexists("paymentMethod","")),
        MenuItemId     = tostring(todynamic(items)["menuItemId"]),
        ItemName       = tostring(todynamic(items)["name"]),
        Size           = tostring(coalesce(todynamic(items)["size"], "")),
        Quantity       = toint(coalesce(todynamic(items)["quantity"], 1)),
        UnitPrice      = todouble(coalesce(todynamic(items)["unitPrice"], 0.0))
    | extend
        LineTotal    = todouble(coalesce(todynamic(items)["totalPrice"], Quantity * UnitPrice)),
        DateKey      = toint(format_datetime(EventTimestamp, "yyyyMMdd")),
        TimeKey      = toint(format_datetime(EventTimestamp, "HHmmss")),
        SourceSystem = "CosmosDB",
        CreatedAt    = EventTimestamp
    | project TransactionId, EventTimestamp, CustomerId, ShopId, AirportId,
              MenuItemId, ItemName, Size, Quantity, UnitPrice, LineTotal,
              PaymentMethod, DateKey, TimeKey, SourceSystem, CreatedAt
}
*+++

1. Select **Run** to execute the code and create the Silver layer functions.
![Screenshot of creating silver layer functions in eventhouse](media/create-silver-layer-functions.png)

1. You can now query the Silver layer views to perform analytics on the ingested streaming data. For example, to get total sales by menu item, use the following query:

+++*
vw_Pos_LineItems()
| summarize TotalSales = sum(LineTotal), TotalQuantity = sum(Quantity) by MenuItemId, ItemName
| order by TotalSales desc
*+++

1. Select **Run** to execute the query and view the results.

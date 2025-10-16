# Lab Overview

Fourth Coffee is a fictional specialty coffee retailer that has carved out a unique niche in the aviation industry by exclusively operating coffee shops within airports. The company has built its business model around serving travelers, airport staff, and aviation professionals who need quality coffee and quick service in fast-paced airport environments.

As Fourth Coffee continues to expand across different airports, the company faces unique challenges in understanding customer behavior patterns, managing inventory across distributed locations, and optimizing store placement within terminal layouts. The company leverages canonical airline and airport data alongside their operational metrics to make data-driven decisions about:

- **Strategic expansion**: Identifying which airports and terminal locations offer the best opportunities for new store openings
- **Personalized customer experiences**: Understanding traveler preferences and flight patterns to deliver targeted recommendations
- **Inventory optimization**: Managing stock levels across multiple airport locations with varying passenger traffic patterns
- **Real-time operations**: Responding quickly to flight delays, gate changes, and peak travel periods that impact customer flow

This lab demonstrates how Fourth Coffee uses Microsoft Fabric and Cosmos DB to unify their operational data with broader aviation analytics, enabling cross-database queries, real-time analytics, and AI-driven insights that power both strategic business decisions and personalized customer experiences.

In this 75 minute lab, you will step into the role of a data engineer for the Forth Coffee Company, unifying operational data with analytical data in Microsoft Fabric. By the end of the lab you will have built an end to end solution that:

- **Ingests and stores operational data** in Cosmos DB in Microsoft Fabric
- **Performs cross-database analytics** by joining operational data in Cosmos DB with curated relational data
- **Processes real-time POS events** using Fabric Eventstreams for streaming analytics and visualization
- **Creates a "gold" personalized insights layer** in a Fabric Data Warehouse for recommendations
- **Implements "reverse ETL"** to push enriched insights back into Cosmos DB for low latency serving to applications
- **TODO: Bonus step**: Add a bonus step here if you have time

This lab contains 5 exercises, each with multiple steps. Complete all the steps in each exercise before moving on to the next one. The exercises are designed to be completed in sequence, building on the work from previous exercises.

1. **Exercise 1**: Provisioning Cosmos DB in Fabric (Operational Data Store)
1. **Exercise 2**: Cross-database analytics with Cosmos DB and Relational Data (Cosmos DB + SQL)
1. **Exercise 3**: Real-Time Streaming and Visualization of POS Events
1. **Exercise 4**: Reverse ETL to Cosmos DB (Operational Serving)
1. **Exercise 5**: TODO: Add a bonus step here if we have time

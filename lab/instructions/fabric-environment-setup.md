# Fabric Environment Setup

In this first part of the lab, you set up the Microsoft Fabric environment needed to complete the exercises in this lab.

This includes creating a new *Fabric workspace*, automated creation of a *Fabric Warehouse*, and loading data into the Data Warehouse that will be used in later exercises.

## Create a new Fabric Workspace

1. In the virtual machine, open a web browser and browse to +++https://app.fabric.microsoft.com+++.

1. When prompted, sign in using the following credentials:

   - **Email**: +++@lab.CloudPortalCredential(User1).Username+++
   - **Password**: +++@lab.CloudPortalCredential(User1).Password+++

1. From the left navigation pane, select **Workspaces**, the select **+ New workspace**.

    ![Screenshot showing how to create a new workspace in Microsoft Fabric](media/create-new-workspace.png)

1. Provide +++*Fourth Coffee Commerce - @lab.LabInstance.Id*+++ as the name for the new workspace, expand the **Advanced** section and for the **License mode** select **Fabric capacity** and in the dropdown select the named Fabric capacity then select **Apply**.

    ![Screenshot showing how to configure the new workspace in Microsoft Fabric](media/configure-new-workspace.png) <!--TODO: No screenshot yet add later-->

## Access the lab repository and set up the Fabric Data Warehouse

On your virtual machine, you will find the lab repo pre-cloned on the desktop under the folder named **Desktop > lab-534**. This is the complete lab repository as available on [our repo](https://aka.ms/). <!--TODO: Add aka.ms link to repo-->

1. Right-click on the **lab-534** folder on the desktop and select **Open in Terminal**.

1. Run the following command to authenticate to Microsft Fabric using Microsoft Entra Id:

    +++*azd auth login*+++

    This will open a new browser window where you can sign in with the following credentials:

    - **Email**: +++@lab.CloudPortalCredential(User1).Username+++
    - **Password**: +++@lab.CloudPortalCredential(User1).Password+++

    Once you have signed in, close the browser window and return to the terminal and confirm that you are successfully authenticated.

1. In the same terminal window, run the following command to set a new environment variable for your Fabric workspace name:

    +++*$env:FABRIC_WORKSPACE_NAME = "Fourth Coffee Commerce - @lab.LabInstance.Id"*+++

1. In the same terminal window, run the following command to execute the C# file that will create the Fabric Data Warehouse and load data into it:

    +++*dotnet run .\src\warehouse_setup\LoadWarehouseData.cs*+++

    This will take a few minutes to complete. Monitor the terminal output for progress and confirmation of completion.

    > [!TIP]
    > Do not close the terminal windows even after completion. You will use this terminal again in later exercises to avoid setting up the environment variable again.

1. Once the script has completed, navigate back to the Fabric Workspace in the web browser and inside the workspace, notice that a new Fabric Data Warehouse named *fc_commerce_wh* has been created. Select it to open.

1. On the left explorer pane, expand **dbo > Tables** to see the list of tables that have been created and loaded with data. Select any table to preview the data.

    ![Screenshot showing the tables in the Fabric Data Warehouse](media/warehouse-tables.png)

## Next step

> Select **Next >** in these instructions to go to the next part of the lab: **Exercise 1: Provisioning Cosmos DB in Fabric (Operational Data Store)**.

import csv
import random
import uuid
from datetime import datetime, timedelta

# -------------------------------
# CONFIGURATION
# -------------------------------
# Possible values for dimensions
payment_methods = ["Credit Card", "Debit Card", "Mobile Pay", "Cash", "Gift Card"]
sizes = ["Small", "Medium", "Large"]
shop_keys = list(range(1, 21))          # 20 shops
menu_item_keys = list(range(1, 51))     # 50 menu items
customer_keys = list(range(1, 501))     # 500 customers

# Date range (last 60 days)
end_date = datetime(2025, 10, 21)
start_date = end_date - timedelta(days=60)

# -------------------------------
# GENERATE TRANSACTIONS
# -------------------------------
transactions = []
for i in range(1, 2001):  # 2,000 transactions
    txn_date = start_date + timedelta(days=random.randint(0, 60))
    txn_time = datetime(
        txn_date.year, txn_date.month, txn_date.day,
        random.randint(8, 22), random.randint(0, 59)
    )

    date_key = int(txn_date.strftime("%Y%m%d"))
    time_key = int(txn_time.strftime("%H%M%S"))
    quantity = random.randint(1, 5)
    unit_price = round(random.uniform(2.5, 8.5), 2)
    total_amount = round(quantity * unit_price, 2)
    loyalty_earned = random.randint(0, 5)
    loyalty_redeemed = random.choice([0, random.randint(0, 3)])
    created_at = txn_time.strftime("%Y-%m-%dT%H:%M:%SZ")

    transactions.append({
        "TransactionId": str(uuid.uuid4()),
        "DateKey": date_key,
        "TimeKey": time_key,
        "CustomerKey": random.choice(customer_keys),
        "ShopKey": random.choice(shop_keys),
        "MenuItemKey": random.choice(menu_item_keys),
        "Quantity": quantity,
        "UnitPrice": unit_price,
        "TotalAmount": total_amount,
        "LoyaltyPointsEarned": loyalty_earned,
        "LoyaltyPointsRedeemed": loyalty_redeemed,
        "PaymentMethod": random.choice(payment_methods),
        "Size": random.choice(sizes),
        "SourceSystem": "CosmosDB",
        "CreatedAt": created_at
    })

# -------------------------------
# WRITE TO CSV
# -------------------------------
output_path = "FactSales_Transactions_2000.csv"
with open(output_path, "w", newline="") as csvfile:
    fieldnames = list(transactions[0].keys())
    writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
    writer.writeheader()
    writer.writerows(transactions)

print(f"✅ 2,000 transaction records written to {output_path}")

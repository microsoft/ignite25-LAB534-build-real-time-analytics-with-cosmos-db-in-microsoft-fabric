import json
import random
import time
from datetime import datetime, timedelta

# Load customer data from customers_container.json
CUSTOMERS_FILE = "../customers_container.json"
with open(CUSTOMERS_FILE, "r") as file:
    customers = json.load(file)

# Load shops data from shops_container.json
SHOPS_FILE = "../shops_container.json"
with open(SHOPS_FILE, "r") as file:
    shops = json.load(file)

# Load menu data from menu_container.json
MENU_FILE = "../menu_container.json"
with open(MENU_FILE, "r") as file:
    menu_items = json.load(file)

# Sample data for randomization
TRANSACTION_TYPES = ["purchase", "refund"]
PAYMENT_METHODS = ["CreditCard", "Cash", "MobilePayment"]

def generate_transaction(customer):
    transaction_id = f"txn-{random.randint(10000, 99999)}"
    timestamp = datetime.utcnow() - timedelta(minutes=random.randint(1, 10000))
    
    # Get customer's preferred airport
    airport_id = customer["preferences"]["airport"]
    
    # Filter shops by customer's preferred airport
    airport_shops = [shop for shop in shops if shop["airportId"] == airport_id]
    
    # If no shops at preferred airport, pick any shop
    if not airport_shops:
        airport_shops = shops
    
    # Select a random shop from the airport
    selected_shop = random.choice(airport_shops)
    shop_id = selected_shop["shopId"]
    
    transaction_type = random.choice(TRANSACTION_TYPES)
    payment_method = random.choice(PAYMENT_METHODS)
    
    # Select random menu items
    num_items = random.randint(1, 4)
    selected_items = random.sample(menu_items, min(num_items, len(menu_items)))
    
    items = []
    for menu_item in selected_items:
        quantity = random.randint(1, 3)
        # Handle items with sizes
        if menu_item.get("sizes") and len(menu_item["sizes"]) > 0:
            size_option = random.choice(menu_item["sizes"])
            unit_price = size_option["price"]
            size_name = size_option["size"]
        else:
            unit_price = menu_item["price"]
            size_name = None
        
        item = {
            "menuItemId": menu_item["menuItemId"],
            "name": menu_item["name"],
            "category": menu_item["category"],
            "quantity": quantity,
            "unitPrice": unit_price,
            "totalPrice": round(quantity * unit_price, 2)
        }
        
        if size_name:
            item["size"] = size_name
        
        items.append(item)
    
    total_amount = round(sum(item["totalPrice"] for item in items), 2)
    loyalty_points_earned = int(total_amount) if transaction_type == "purchase" else 0
    loyalty_points_redeemed = random.randint(0, min(10, customer.get("loyaltyPoints", 0))) if transaction_type == "purchase" else 0
    status = "completed" if transaction_type == "purchase" else "refunded"
    
    metadata = {
        "deviceId": f"pos-terminal-{random.randint(1, 15):02d}",
        "employeeId": f"emp-{random.randint(100, 999)}",
        "orderNumber": f"ORD-{timestamp.strftime('%Y%m%d')}-{random.randint(1000, 9999)}"
    }
    
    return {
        "id": transaction_id,
        "transactionId": transaction_id,
        "timestamp": timestamp.isoformat() + "Z",
        "customerId": customer["customerId"],
        "shopId": shop_id,
        "airportId": airport_id,
        "transactionType": transaction_type,
        "items": items,
        "totalAmount": total_amount,
        "paymentMethod": payment_method,
        "loyaltyPointsEarned": loyalty_points_earned,
        "loyaltyPointsRedeemed": loyalty_points_redeemed,
        "status": status,
        "metadata": metadata,
        "_partitionKey": shop_id
    }

def stream_transactions():
    while True:
        customer = random.choice(customers)
        transaction = generate_transaction(customer)
        print(json.dumps(transaction, indent=2))
        time.sleep(1)  # Stream a transaction every second

if __name__ == "__main__":
    stream_transactions()
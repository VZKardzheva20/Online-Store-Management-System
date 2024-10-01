using System;
using System.Collections.Generic;
using System.Linq;

namespace OnlineStoreManagementSystem
{
    public abstract class Product
    {
        public string Name { get; protected set; }
        public decimal Price { get; protected set; }
        public int Stock { get; protected set; }

        public event EventHandler<OutOfStockEventArgs> OutOfStock;

        protected Product(string name, decimal price, int stock)
        {
            Name = name;
            Price = price;
            Stock = stock;
        }

        public virtual bool DeductStock(int quantity)
        {
            if (quantity <= Stock)
            {
                Stock -= quantity;
                if (Stock == 0)
                {
                    OnOutOfStock();
                }
                return true;
            }
            return false;
        }

        public virtual void RestoreStock(int quantity)
        {
            Stock += quantity;
            Logger.Log($"Restored {quantity} units to {Name}. New stock: {Stock}");
        }

        protected virtual void OnOutOfStock()
        {
            OutOfStock?.Invoke(this, new OutOfStockEventArgs(Name));
        }

        public abstract string DisplayDetails();
    }

    public class OutOfStockEventArgs : EventArgs
    {
        public string ProductName { get; }

        public OutOfStockEventArgs(string productName)
        {
            ProductName = productName;
        }
    }

    public class PhysicalProduct : Product
    {
        public double Weight { get; private set; }

        public PhysicalProduct(string name, decimal price, int stock, double weight)
            : base(name, price, stock)
        {
            Weight = weight;
        }

        public override string DisplayDetails()
        {
            return $"Physical Product: {Name}, Price: ${Price}, Stock: {Stock}, Weight: {Weight}kg";
        }
    }

    public class DigitalProduct : Product
    {
        public string DownloadLink { get; private set; }

        public DigitalProduct(string name, decimal price, int stock, string downloadLink)
            : base(name, price, stock)
        {
            DownloadLink = downloadLink;
        }

        public override string DisplayDetails()
        {
            return $"Digital Product: {Name}, Price: ${Price}, Stock: {Stock}, Download Link: {DownloadLink}";
        }
    }

    public class Customer
    {
        public string FirstName { get; private set; }
        public string LastName { get; private set; }

        public Customer(string firstName, string lastName)
        {
            FirstName = firstName;
            LastName = lastName;
        }

        public string DisplayInfo()
        {
            return $"{FirstName} {LastName}";
        }
    }
    
    public interface IDiscount
    {
        decimal ApplyDiscount(decimal originalPrice, int quantity = 1);
    }
   
    public class FixedDiscount : IDiscount
    {
        private readonly decimal _discountAmount;
   
        public FixedDiscount(decimal discountAmount)
        {
            _discountAmount = discountAmount > 0 ? discountAmount : 0;
        }
   
        public decimal ApplyDiscount(decimal originalPrice, int quantity = 1)
        {
            return Math.Max(originalPrice - (_discountAmount * quantity), 0);
        }
    }
   
    public class PercentageDiscount : IDiscount
    {
        private readonly decimal _percentage;
   
        public PercentageDiscount(decimal percentage)
        {
            _percentage = Math.Clamp(percentage, 0, 100);
        }
   
        public decimal ApplyDiscount(decimal originalPrice, int quantity = 1)
        {
            return originalPrice * (1 - _percentage / 100);
        }
    }
   
    public class BulkDiscount : IDiscount
    {
        private readonly int _minimumQuantity;
        private readonly decimal _percentage;
   
        public BulkDiscount(int minimumQuantity, decimal percentage)
        {
            _minimumQuantity = minimumQuantity > 0 ? minimumQuantity : 1;
            _percentage = Math.Clamp(percentage, 0, 100);
        }
   
        public decimal ApplyDiscount(decimal originalPrice, int quantity = 1)
        {
            if (quantity >= _minimumQuantity)
            {
                return originalPrice * (1 - _percentage / 100);
            }
            return originalPrice;
        }
    }

    public interface IPayment
    {
        bool ProcessPayment(decimal amount);
    }

    public class CreditCardPayment : IPayment
    {
        public bool ProcessPayment(decimal amount)
        {
            Logger.Log($"Processing credit card payment for ${amount}");
            return true;
        }
    }

    public class PayPalPayment : IPayment
    {
        public bool ProcessPayment(decimal amount)
        {
            Logger.Log($"Processing PayPal payment for ${amount}");
            return true;
        }
    }
   
    public class OrderItem
    {
        public Product Product { get; }
        public int Quantity { get; }
        public decimal OriginalPrice { get; }
        public decimal DiscountedPrice { get; private set; }
   
        public OrderItem(Product product, int quantity)
        {
            Product = product;
            Quantity = quantity;
            OriginalPrice = product.Price * quantity;
            DiscountedPrice = OriginalPrice;
        }
   
        public void ApplyDiscount(IDiscount discount)
        {
            DiscountedPrice = discount.ApplyDiscount(OriginalPrice, Quantity);
        }
    }
   
    public class Order
    {
        public string OrderId { get; }
        public Customer Customer { get; }
        public List<OrderItem> Items { get; }
        public OrderStatus Status { get; private set; }
        public decimal TotalPrice => Items.Sum(item => item.DiscountedPrice);
   
        public Order(Customer customer)
        {
            OrderId = Guid.NewGuid().ToString("N");
            Customer = customer;
            Items = new List<OrderItem>();
            Status = OrderStatus.Created;
        }
   
        public bool AddItem(Product product, int quantity)
        {
            if (product.Stock >= quantity)
            {
                Items.Add(new OrderItem(product, quantity));
                return true;
            }
            Logger.Log($"Failed to add item: {product.Name}. Insufficient stock.");
            return false;
        }
   
        public void ApplyDiscount(IDiscount discount)
        {
            foreach (var item in Items)
            {
                item.ApplyDiscount(discount);
            }
        }
   
        public bool ProcessOrder(IPayment paymentMethod)
        {
            if (Status != OrderStatus.Created)
            {
                Logger.Log($"Cannot process order {OrderId}. Current status: {Status}");
                return false;
            }
   
            foreach (var item in Items)
            {
                if (!item.Product.DeductStock(item.Quantity))
                {
                    Logger.Log($"Failed to process order {OrderId}. Insufficient stock for {item.Product.Name}");
                    RestoreAllStock();
                    return false;
                }
            }
   
            if (paymentMethod.ProcessPayment(TotalPrice))
            {
                Status = OrderStatus.Completed;
                Logger.Log($"Order {OrderId} processed successfully. Total amount: ${TotalPrice}");
                return true;
            }
   
            RestoreAllStock();
            Logger.Log($"Payment failed for order {OrderId}. All stock has been restored.");
            return false;
        }
   
        public bool CancelOrder()
        {
            if (Status != OrderStatus.Completed)
            {
                Logger.Log($"Cannot cancel order {OrderId}. Current status: {Status}");
                return false;
            }
   
            RestoreAllStock();
            Status = OrderStatus.Cancelled;
            Logger.Log($"Order {OrderId} cancelled successfully");
            return true;
        }

        private void RestoreAllStock()
        {
            foreach (var item in Items)
            {
                item.Product.RestoreStock(item.Quantity);
            }
        }
    }
   
    public enum OrderStatus
    {
        Created,
        Completed,
        Cancelled
    }

    public static class Logger
    {
        private static List<string> _logs = new List<string>();
   
        public static void Log(string message)
        {
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            _logs.Add(logEntry);
            Console.WriteLine(logEntry);
        }
   
        public static List<string> GetLogs()
        {
            return new List<string>(_logs);
        }
    }
   
    public class Store
    {
        private List<Product> _products = new List<Product>();
        private List<Customer> _customers = new List<Customer>();
        private List<Order> _orders = new List<Order>();
   
        public Store()
        {
            InitializeStore();
        }
   
        private void InitializeStore()
        {
            var phone = new PhysicalProduct("Smartphone", 999.99m, 10, 0.2);
            var laptop = new PhysicalProduct("Laptop", 1499.99m, 5, 2.0);
            var ebook = new DigitalProduct("C# Programming Guide", 29.99m, 100, "download.example.com/ebook");
   
            phone.OutOfStock += HandleOutOfStock;
            laptop.OutOfStock += HandleOutOfStock;
            ebook.OutOfStock += HandleOutOfStock;
   
            _products.AddRange(new[] { phone, laptop, ebook });
            _customers.Add(new Customer("John", "Doe"));
        }
   
        private void HandleOutOfStock(object sender, OutOfStockEventArgs e)
        {
            Logger.Log($"Alert: {e.ProductName} is out of stock!");
        }
   
        public void DemonstrateAllFeatures()
        {
            var customer = _customers[0];
            Logger.Log($"Customer: {customer.DisplayInfo()}");
   
            var order = new Order(customer);
            bool addItemResult = order.AddItem(_products[0], 2);
            Logger.Log($"Adding 2 smartphones to order: {(addItemResult ? "Success" : "Failed")}");
            
            addItemResult = order.AddItem(_products[1], 1);
            Logger.Log($"Adding 1 laptop to order: {(addItemResult ? "Success" : "Failed")}");
   
            var regularDiscount = new PercentageDiscount(10);
            var bulkDiscount = new BulkDiscount(2, 15);
   
            Logger.Log("\nApplying discounts:");
            order.ApplyDiscount(regularDiscount);
            order.ApplyDiscount(bulkDiscount);
   
            Logger.Log("\nProcessing order:");
            if (order.ProcessOrder(new CreditCardPayment()))
            {
                _orders.Add(order);
                Logger.Log($"Order completed! Order ID: {order.OrderId}, Total price: ${order.TotalPrice}");
            }
   
            Logger.Log("\nCancelling order:");
            if (order.CancelOrder())
            {
                Logger.Log("Order cancelled successfully. Stock has been restored.");
            }
   
            Logger.Log("\nAttempting to process cancelled order:");
            if (!order.ProcessOrder(new PayPalPayment()))
            {
                Logger.Log("Failed to process cancelled order, as expected.");
            }
   
            Logger.Log("\nSystem Logs:");
            foreach (var log in Logger.GetLogs())
            {
                Console.WriteLine(log);
            }
        }
    }
   
    public class Program
    {
        public static void Main(string[] args)
        {
            var store = new Store();
            store.DemonstrateAllFeatures();
        }
    }
}
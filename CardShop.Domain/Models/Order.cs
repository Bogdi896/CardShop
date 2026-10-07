using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CardShop.Domain.Enums;

namespace CardShop.Domain.Models
{
    public class Order
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public List<OrderItem> Items { get; set; } = new();
    }
}

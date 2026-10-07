using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CardShop.Domain.Enums;

namespace CardShop.Domain.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ProductType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int AvailableQuantity { get; set; } = 0;
        public bool IsActive { get; set; } = true;

    }
}

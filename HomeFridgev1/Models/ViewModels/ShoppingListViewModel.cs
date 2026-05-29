using System.Collections.Generic;
using HomeFridgev1.Models.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HomeFridgev1.Models.ViewModels
{
    public class ShoppingListViewModel
    {
        public List<ShoppingItem> ActiveItems { get; set; } = new();
        public List<ShoppingItem> PurchasedItems { get; set; } = new();
        public List<ShoppingSuggestionViewModel> Suggestions { get; set; } = new();
        
        public List<SelectListItem> Categories { get; set; } = new();
        public List<SelectListItem> StorageLocations { get; set; } = new();
        
        // Form properties for the inline/modal Quick-Add
        public ShoppingItemFormViewModel NewItem { get; set; } = new();
    }

    public class ShoppingSuggestionViewModel
    {
        public int FoodItemId { get; set; }
        public string FoodName { get; set; } = string.Empty;
        public decimal CurrentQuantity { get; set; }
        public decimal MinQuantityThreshold { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}

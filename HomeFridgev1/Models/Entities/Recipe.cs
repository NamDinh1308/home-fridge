using System;
using System.Collections.Generic;

namespace HomeFridgev1.Models.Entities
{
    public class Recipe
    {
        public int Id { get; set; }

        public int HouseholdId { get; set; }
        public Household? Household { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Instructions { get; set; }

        public string? ImagePath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    }
}

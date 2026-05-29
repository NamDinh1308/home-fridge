namespace HomeFridgev1.Models.Entities
{
    public class RecipeIngredient
    {
        public int Id { get; set; }

        public int RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        public string FoodName { get; set; } = string.Empty;

        public decimal RequiredQuantity { get; set; }

        public string Unit { get; set; } = string.Empty;
    }
}

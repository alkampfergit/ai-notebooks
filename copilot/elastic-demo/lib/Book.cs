using System;

namespace lib;

public class Book
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public DateTime PublicationDate { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public string Genre { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Language { get; set; } = "English";
    public bool IsAvailable { get; set; } = true;
}
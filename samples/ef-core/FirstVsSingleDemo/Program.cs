using FirstVsSingleDemo;
using Microsoft.EntityFrameworkCore;

await using var db = new AppDbContext();
await db.Database.EnsureDeletedAsync();
await db.Database.EnsureCreatedAsync();

// init dirty data
await db.Users.AddRangeAsync(
    new User { Email = "test@example.com" },
    new User { Email = "test@example.com" }
);
    
await db.SaveChangesAsync();
    
// using FirstOrDefaultAsync
Console.WriteLine("===== FirstOrDefaultAsync =====");
var first = await db.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
Console.WriteLine(first is null
    ? "No User found"
    : $"user found: {first.Email}");

// using SingleOrDefaultAsync
try
{
    Console.WriteLine("===== SingleOrDefaultAsync =====");
    var single = await db.Users.SingleOrDefaultAsync(u => u.Email == "test@example.com");
    Console.WriteLine(single is null
        ? "No User found"
        : $"user found: {single.Email}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine("Exception thrown:");
    Console.WriteLine(ex.Message);
}



using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public enum CpuSocket
{
    [Display(Name = "Intel LGA 1200")] Lga1200,
    [Display(Name = "Intel LGA 1700")] Lga1700,
    [Display(Name = "Intel LGA 1851")] Lga1851,
    [Display(Name = "AMD AM4")] Am4,
    [Display(Name = "AMD AM5")] Am5
}

public enum MemoryStandard
{
    [Display(Name = "DDR4")] Ddr4,
    [Display(Name = "DDR5")] Ddr5
}

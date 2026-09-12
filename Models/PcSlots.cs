namespace EcommerceApp.Models;

public static class PcSlots
{
    public static readonly string[] All = { "CPU", "VGA", "RAM", "SSD", "Mainboard", "PSU", "Case", "Cooling" };
    public static readonly string[] Required = { "CPU", "Mainboard", "RAM", "SSD", "PSU", "Case" };
    public static readonly string[] Optional = { "VGA", "Cooling" };
}

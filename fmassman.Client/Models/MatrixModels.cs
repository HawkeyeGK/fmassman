namespace fmassman.Client.Models;

public record MatrixColumn(string Key, string Name, string Category, string Phase = "");

public class MatrixItem 
{ 
    public string Name { get; set; } = ""; 
    public List<string> TagIds { get; set; } = new(); 
    public Dictionary<string, double> Scores { get; set; } = new(); 
    public double GetScore(string key) => Scores.TryGetValue(key, out var val) ? val : 0; 
}

namespace EkstraSim.Prediction.Models;

public sealed record FormFactors(double Recent, double Venue, double HeadToHead)
{
    public double Product => Recent * Venue * HeadToHead;
}

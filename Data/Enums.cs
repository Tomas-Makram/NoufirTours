namespace NoufirTours.Data
{
    public enum UserRole
    {
        Admin = 0,
        Driver = 1,
        Staff = 2
    }

    public enum TripOrigin
    {
        Manual = 0,
        AutoPlan = 1
    }

    public enum AutoPlanScheduleType
    {
        Daily = 1,
        SpecificDate = 2
    }

    public enum AutoPlanActivationMode
    {
        ParallelAllActive = 1,
        SequentialByCapacity = 2,
    }

    public enum TripPlaceType
    {
        Pickup = 1,
        Dropoff = 2,
        Stop = 3
    }

    public enum SeatLeg
    {
        Go = 1,
        Return = 2
    }

    public enum TripPriceType
    {
        Go = 1,
        Return = 2,
        Round = 3
    }
}
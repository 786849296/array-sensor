namespace array_sensor.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}

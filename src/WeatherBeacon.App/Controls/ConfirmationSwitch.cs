using System.Windows.Controls.Primitives;
namespace WeatherBeacon.Controls;
// Activation is committed by the confirmation workflow, never by the initial click.
public sealed class ConfirmationSwitch : ToggleButton
{
 protected override void OnToggle() { }
}

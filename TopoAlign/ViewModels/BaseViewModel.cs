using CommunityToolkit.Mvvm.ComponentModel;

namespace TopoAlign.ViewModels;

public class BaseViewModel : ObservableValidator
    {
        public event EventHandler ClosingRequest;

        protected void OnClosingRequest()
        {
            if (this.ClosingRequest != null)
            {
                this.ClosingRequest(this, EventArgs.Empty);
            }
        }
    }


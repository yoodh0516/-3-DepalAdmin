using System.Windows.Controls;
using System.Windows.Threading;

namespace DepalAdmin.Feature.Chat;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is ChatViewModel vm)
                vm.ScrollToEndRequested += (_, _) =>
                    Dispatcher.InvokeAsync(() => Scroller.ScrollToEnd(), DispatcherPriority.Background);
        };
    }
}

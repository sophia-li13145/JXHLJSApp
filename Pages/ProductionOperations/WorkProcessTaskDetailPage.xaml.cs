using JXHLJSApp.Models;
using JXHLJSApp.ViewModels;

namespace JXHLJSApp.Pages;

public partial class WorkProcessTaskDetailPage : ContentPage
{
    private readonly WorkProcessTaskDetailViewModel _vm;
    /// <summary>执行 WorkProcessTaskDetailPage 初始化逻辑。</summary>
    public WorkProcessTaskDetailPage() : this(ServiceHelper.GetService<WorkProcessTaskDetailViewModel>()) { }

    /// <summary>执行 WorkProcessTaskDetailPage 初始化逻辑。</summary>
    public WorkProcessTaskDetailPage(WorkProcessTaskDetailViewModel vm)
    {
        InitializeComponent();
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        BindingContext = _vm;
    }

    /// <summary>执行 OnReportQtyCompleted 逻辑。</summary>
    private void OnReportQtyCompleted(object sender, EventArgs e)
    {

            _vm.SubmitReportQtyCommand.Execute(null);
        
    }


}

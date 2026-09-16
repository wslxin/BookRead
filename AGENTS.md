# Repository Guidelines

## 项目结构与模块组织

`BookRead/` 是 .NET 9 WPF 桌面应用，项目文件为 `BookRead/BookRead.csproj`。

- `Pages/`：书架、阅读和设置页面；`Controls/`、`Dialogs/`：可复用控件与对话框。
- `Models/`：数据模型和事件参数；`Services/`：持久化、解析、主题及快捷键逻辑。
- `*.xaml` 定义界面，配对的 `*.xaml.cs` 放置代码后置；`readbook.ico` 是应用图标。
- `bin/`、`obj/` 为生成目录，不应提交。

## 构建、测试与开发命令

在 Windows 和 .NET 9 SDK 环境下，从仓库根目录执行：

```powershell
dotnet restore BookRead/BookRead.csproj
dotnet build BookRead/BookRead.csproj
dotnet run --project BookRead/BookRead.csproj
```

上述命令分别用于还原依赖、编译和启动应用。目前没有测试项目；新增后可从根目录执行 `dotnet test`。

## 编码风格与命名约定

C# 使用四个空格缩进、可空引用类型和文件范围命名空间。公共类型与成员使用 PascalCase，
局部变量和参数使用 camelCase，私有字段使用 `_camelCase`。XAML 与代码后置文件应同名。
新增或修改的方法必须添加 XML 文档注释；非显而易见的业务约束使用中文行内注释说明原因。
事件处理器保持简短，可复用逻辑放入 `Services/` 或 `Models/`。

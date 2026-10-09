using MudBlazor;
using Relewise.BlazorApps.XmlSummaries;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Relewise.Client;
using System.IO.Compression;
using System.Reflection;

namespace Relewise.BlazorApps.TypeEditors
{
    public partial class ObjectEditor<T> : ComponentBase
    {
        [Inject] public required ISnackbar Snackbar { get; set; }
        private bool hasBeenOpen;
        private object? emptyInstanceOfValue;
        private Type? emptyInstanceType;
        private List<Type> derivedTypes = new();
        private static readonly Lazy<Type[]> derivedTypeCandidates = new(() => EditorSdkTypes.All.Value
            .Where(type => type != typeof(T) && type.IsAssignableTo(typeof(T)) && !type.IsGenericType && !type.IsAbstract)
            .ToArray());
        private Type selectedCreateType = typeof(T);
        private XmlDocumentation? xmlDocumentation;
        private CommunityDocumentation? communityDocumentation;

        [Parameter]
        public bool ShowModelShareLink { get; set; } = false;

        [Parameter]
        public bool ReadOnly { get; set; } = false;

        [Parameter]
        public bool OpenDetails { get; set; } = false;

        [Parameter]
        public bool SingleLevelOpenDetails { get; set; } = false;

        [Parameter, CascadingParameter]
        public bool? HideDefaultValueProperties { get; set; }

        [Parameter]
        public bool? OverridenHideDefaultValueProperties { get; set; }

        [Parameter, EditorRequired]
        public required object? Value { get; set; }

        [Parameter, EditorRequired]
        public required Action<object?> Setter { get; set; }

        [Parameter]
        public Type[] AncestorTypes { get; set; } = [];

        [Parameter]
        public Type[]? AllowedDerivedTypes { get; set; }

        [Inject]
        public required IJSRuntime JSRuntime { get; set; }

        [Inject]
        public required HttpClient HttpClient { get; set; }

        [Inject]
        public required NavigationManager NavigationManager { get; set; }

        [Inject]
        public required DocumentationCache XMLDocumentationCache { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            hasBeenOpen = hasBeenOpen || OpenDetails || SingleLevelOpenDetails;

            if (OverridenHideDefaultValueProperties is not null)
            {
                HideDefaultValueProperties = OverridenHideDefaultValueProperties;
            }

            (xmlDocumentation, communityDocumentation) = await XMLDocumentationCache.GetAsync();

            derivedTypes = derivedTypeCandidates.Value.ToList();

            if (AllowedDerivedTypes is not null)
                derivedTypes = derivedTypes.Where(type => AllowedDerivedTypes.Contains(type)).ToList();

            if (Settings.TypeInheritanceNotIgnored.Concat(Settings.TypeInheritanceSupported).FirstOrDefault(l => typeof(T).IsAssignableTo(l.BaseType) && AncestorTypes.Any(a => l.AncestorInterfaces.Any(ai => a.IsAssignableTo(ai)))) is { } limiter)
            {
                derivedTypes = derivedTypes
                    .Where(t => limiter.TypeInhertianceLimit.Any(l => t.IsAssignableTo(l)))
                    .ToList();
            }

            if (derivedTypes.Count > 0 && (typeof(T).IsGenericType || typeof(T).IsAbstract))
            {
                if (Value is not null && Value.GetType() != typeof(T))
                {
                    selectedCreateType = Value.GetType();
                }
                else
                {
                    selectedCreateType = derivedTypes.First();
                }
            }

            Type? editedTyped = null;
            if (Value is not null)
            {
                editedTyped = Value.GetType();
            }
            else if (selectedCreateType is not null)
            {
                editedTyped = selectedCreateType;
            }
            if (editedTyped is not null && editedTyped != emptyInstanceType)
            {
                var editor = Settings.Editors.First(e => e.CanHandle(editedTyped));
                emptyInstanceOfValue = editor.InitValue(editedTyped);
                emptyInstanceType = editedTyped;
            }
        }

        private void TypeDropdownChanged(ChangeEventArgs eventArgs)
        {
            selectedCreateType = derivedTypes.First(t => t.Name == (string?)eventArgs.Value);
        }

        public void Create()
        {
            try
            {
                var editor = Settings.Editors.First(e => e.CanHandle(selectedCreateType));
                Value = editor.InitValue(selectedCreateType);
                emptyInstanceOfValue = editor.InitValue(selectedCreateType);
                emptyInstanceType = selectedCreateType;
                Setter(Value);
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        private void RemoveProperty(PropertyInfo propertyInfo)
        {
            try
            {
                propertyInfo.SetValue(Value, null);
                Setter(Value);
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        public async Task CopyToClipboard()
        {
            try
            {
                var jsonSerializerSettings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                };
                jsonSerializerSettings.Converters.Add(new StringEnumConverter());

                Type? serializedType = Value?.GetType();
                if (serializedType?.BaseType is not null)
                {
                    serializedType = serializedType.BaseType;
                }

                await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", JsonConvert.SerializeObject(Value, serializedType, jsonSerializerSettings));
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        public async Task CopyLinkToClipboard()
        {
            try
            {
                var jsonSerializerSettings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.None,
                    Converters = [new StringEnumConverter()]
                };

                Type? serializedType = Value?.GetType();
                if (serializedType?.BaseType is not null)
                {
                    serializedType = serializedType.BaseType;
                }

                string serialized = JsonConvert.SerializeObject(Value, serializedType, jsonSerializerSettings);
                string compressed = ToGzip(serialized);

                string url = $"{NavigationManager.BaseUri}Models?q={Value?.GetType().Name}&o={compressed}";

                await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", url);
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        public static string ToGzip(string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            using var input = new MemoryStream(bytes);
            using var output = new MemoryStream();
            using var stream = new GZipStream(output, CompressionLevel.Fastest);

            input.CopyTo(stream);

            stream.Dispose();

            var result = output.ToArray();
            return Convert.ToBase64String(result);
        }

        private IEnumerable<PropertyInfo>? Properties => Value is null ? null : Settings.GetProperties(Value.GetType());
    }

    internal static class EditorSdkTypes
    {
        internal static readonly Lazy<Type[]> All = new(() => typeof(ClientBase).Assembly.GetTypes());
    }
}

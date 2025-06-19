using KristofferStrube.Blazor.Relewise.XmlSummaries;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Relewise.Client;
using System.IO.Compression;
using System.Reflection;

namespace KristofferStrube.Blazor.Relewise.TypeEditors
{
    public partial class ObjectEditor<T> : ComponentBase
    {
        private bool hasBeenOpen;
        private object? emptyInstanceOfValue;
        private List<Type> derivedTypes = new();
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

        [Inject]
        public required IJSRuntime JSRuntime { get; set; }

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

            var assembly = Assembly.GetAssembly(typeof(ClientBase));
            derivedTypes = assembly!
                .GetTypes()
                .Where(type => type != typeof(T) && type.IsAssignableTo(typeof(T)) && !type.IsGenericType && !type.IsAbstract)
                .ToList();

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
            if (editedTyped is not null)
            {
                var editor = Settings.Editors.First(e => e.CanHandle(editedTyped));
                emptyInstanceOfValue = editor.InitValue(editedTyped);
            }
        }

        private void TypeDropdownChanged(ChangeEventArgs eventArgs)
        {
            selectedCreateType = derivedTypes.First(t => t.Name == (string?)eventArgs.Value);
        }

        public void Create()
        {
            var editor = Settings.Editors.First(e => e.CanHandle(selectedCreateType));
            Value = editor.InitValue(selectedCreateType);
            emptyInstanceOfValue = editor.InitValue(selectedCreateType);
            Setter(Value);
        }

        private void RemoveProperty(PropertyInfo propertyInfo)
        {
            propertyInfo.SetValue(Value, null);
            Setter(Value);
        }

        public async Task CopyToClipboard()
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

        public async Task CopyLinkToClipboard()
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
            Console.WriteLine(serialized);
            string compressed = ToGzip(serialized);

            string url = $"{NavigationManager.BaseUri}Models?q={Value?.GetType().Name}&o={compressed}";

            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", url);
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
}
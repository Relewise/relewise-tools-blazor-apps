using MudBlazor;
using Relewise.BlazorApps.XmlSummaries;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Recommendations;
using System.IO.Compression;
using System.Reflection;

namespace Relewise.BlazorApps.TypeEditors
{
    public partial class ObjectEditor<T> : ComponentBase
    {
        [Inject] public required ISnackbar Snackbar { get; set; }
        private const string DisableMerchandisingKey = "Debug_DisableMerchandising";
        private bool hasBeenOpen;
        private bool openCreatedValue;
        private bool typeMenuOpen;
        private bool IsTopLevel => AncestorTypes.Length == 0;
        private object? emptyInstanceOfValue;
        private Type? emptyInstanceType;
        private List<Type> derivedTypes = new();
        private bool CanSwitchType => Value is not null && derivedTypes.Any(type => type != Value.GetType());
        private static readonly Lazy<Type[]> derivedTypeCandidates = new(() => EditorSdkTypes.All.Value
            .Where(type => type != typeof(T) && type.IsAssignableTo(typeof(T)) && !type.IsGenericType && !type.IsAbstract)
            .ToArray());
        private Type selectedCreateType = typeof(T);
        private XmlDocumentation? xmlDocumentation;
        private XmlDocumentation? typeTitleDocumentation;
        private readonly Dictionary<Type, string> typeTitles = new();
        private CommunityDocumentation? communityDocumentation;
        private XmlDocumentation? propertyHelpDocumentation;
        private CommunityDocumentation? propertyHelpCommunityDocumentation;
        private readonly Dictionary<PropertyInfo, string?> propertyHelp = new();
        private Type? propertiesType;
        private PropertyInfo[] properties = [];

        [Parameter]
        public bool ShowModelShareLink { get; set; } = false;

        [Parameter]
        public bool ReadOnly { get; set; } = false;

        [Parameter]
        public bool OpenDetails { get; set; } = false;

        [Parameter]
        public bool SingleLevelOpenDetails { get; set; } = false;

        [Parameter]
        public bool FieldsOnly { get; set; } = false;

        [Parameter] public RenderFragment? HeaderActions { get; set; }

        [Parameter] public EventCallback OnRemove { get; set; }
        [Parameter] public string? RemoveLabel { get; set; }

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

            // Initialize field visibility before optional documentation can delay rendering.
            (xmlDocumentation, communityDocumentation) = await XMLDocumentationCache.GetAsync();
        }

        private void TypeDropdownChanged(ChangeEventArgs eventArgs)
        {
            selectedCreateType = derivedTypes.First(t => t.Name == (string?)eventArgs.Value);
        }

        private void SwitchType(Type type)
        {
            selectedCreateType = type;
            typeMenuOpen = false;
            Create();
        }

        private void DefineNullObject()
        {
            if (ReadOnly || Value is not null) return;
            if (derivedTypes.Count > 1)
                typeMenuOpen = true;
            else
                Create();
        }

        private void NullObjectKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs args)
        {
            if (args.Key is "Enter" or " ")
                DefineNullObject();
        }

        private string GetTypeTitle(Type type)
        {
            // A failed documentation load can succeed later. Do not retain missing
            // titles when the documentation instance changes after a retry.
            if (!ReferenceEquals(typeTitleDocumentation, xmlDocumentation))
            {
                typeTitles.Clear();
                typeTitleDocumentation = xmlDocumentation;
            }

            if (!typeTitles.TryGetValue(type, out var title))
            {
                title = xmlDocumentation?.GetSummary(type) ?? "";
                typeTitles.Add(type, title);
            }

            return title;
        }

        private string? GetPropertyHelp(PropertyInfo property)
        {
            if (!ReferenceEquals(propertyHelpDocumentation, xmlDocumentation)
                || !ReferenceEquals(propertyHelpCommunityDocumentation, communityDocumentation))
            {
                propertyHelp.Clear();
                propertyHelpDocumentation = xmlDocumentation;
                propertyHelpCommunityDocumentation = communityDocumentation;
            }

            if (!propertyHelp.TryGetValue(property, out var help))
            {
                help = property.GetCustomAttribute<ObsoleteAttribute>()?.Message
                    ?? xmlDocumentation?.GetSummary(property.DeclaringType!, property.Name)
                    ?? communityDocumentation?.GetSummary(property.DeclaringType!, property.Name);
                propertyHelp.Add(property, help);
            }
            return help;
        }

        private static bool IsInlineValueType(Type type) => type == typeof(Language) || type == typeof(Currency);

        private Type InlineValueType => Value?.GetType() ?? typeof(T);

        private string? InlineValue => Value switch
        {
            Language language => language.Value,
            Currency currency => currency.Value,
            _ => null
        };

        private void UpdateInlineValue(object? value)
        {
            if (ReadOnly) return;
            try
            {
                bool isCurrency = InlineValueType == typeof(Currency);
                Value = value is string text && !string.IsNullOrWhiteSpace(text)
                    ? isCurrency ? (object)new Currency(text) : new Language(text)
                    : null;
                Setter(Value);
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        public void Create()
        {
            if (ReadOnly) return;
            try
            {
                var editor = Settings.Editors.First(e => e.CanHandle(selectedCreateType));
                Value = editor.InitValue(selectedCreateType);
                emptyInstanceOfValue = editor.InitValue(selectedCreateType);
                emptyInstanceType = selectedCreateType;
                hasBeenOpen = true;
                openCreatedValue = true;
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

        private void UpdateProperty(PropertyInfo property, object? value)
        {
            property.SetValue(Value, value);
            Setter(Value);
            // These fields also affect the object's header or dynamic help text.
            if (property.Name == "Id" || property.Name.Contains("Minutes") || IsRequestCustom(property)) StateHasChanged();
        }

        private bool IsRequestCustom(PropertyInfo property) => property.Name == nameof(LicensedRequest.Custom)
            && Value is SearchRequest or RecommendationRequest;

        private void SetDisableMerchandising(LicensedRequest request, bool disabled)
        {
            if (ReadOnly) return;
            Dictionary<string, string> custom = request.Custom is null ? new() : new(request.Custom, request.Custom.Comparer);
            if (disabled)
                custom[DisableMerchandisingKey] = "true";
            else
                custom.Remove(DisableMerchandisingKey);
            request.Custom = custom;
            Setter(Value);
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

                string url = Shared.ModelShareLink.Create(NavigationManager.BaseUri, Value!.GetType(), compressed);

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

        private bool ShouldShowProperty(PropertyInfo property)
            => HideDefaultValueProperties is not true
                || (Value is null && emptyInstanceOfValue is null)
                || (Value is not null && emptyInstanceOfValue is not null
                    && JsonConvert.SerializeObject(property.GetValue(Value)) != JsonConvert.SerializeObject(property.GetValue(emptyInstanceOfValue))
                    && !Settings.InvalidProperties.Any(invalid => property == invalid.Property
                        && AncestorTypes.Any(ancestor => invalid.AncestorInterfaces.Any(type => ancestor.IsAssignableTo(type)))));

        private PropertyInfo[] Properties
        {
            get
            {
                if (Value?.GetType() != propertiesType)
                {
                    propertiesType = Value?.GetType();
                    properties = propertiesType is null ? [] : Settings.GetProperties(propertiesType).ToArray();
                    propertyHelp.Clear();
                }
                return properties;
            }
        }
    }

    internal static class EditorSdkTypes
    {
        internal static readonly Lazy<Type[]> All = new(() => typeof(ClientBase).Assembly.GetTypes());
    }
}

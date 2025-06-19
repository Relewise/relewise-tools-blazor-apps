using KristofferStrube.Blazor.Relewise.TypeEditors;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Client.DataTypes.Triggers.Configurations;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.RelevanceModifiers;
using Relewise.Client.Requests.Search;
using System.Reflection;

namespace KristofferStrube.Blazor.Relewise;

public static class Settings
{
    public static readonly List<EditorHandler> Editors =
    [
        new(t => t.IsEnum,
            t => typeof(EnumEditor<>).MakeGenericType(new Type[] { t }),
            t => Enum.ToObject(t, 0)),
        new(t => t == typeof(double),
            _ => typeof(DoubleEditor),
            _ => 0.0),
        new(t => t == typeof(float),
            _ => typeof(FloatEditor),
            _ => 0.0f),
        new(t => t == typeof(decimal),
            _ => typeof(DecimalEditor),
            _ => 0M),
        new(t => t == typeof(ushort),
            _ => typeof(UshortEditor),
            _ => 0),
        new(t => t == typeof(int),
            _ => typeof(IntEditor),
            _ => 0),
        new(t => t == typeof(long),
            _ => typeof(LongEditor),
            _ => 0),
        new(t => t == typeof(string),
            _ => typeof(StringEditor),
            _ => ""),
        new(t => t == typeof(Guid),
            _ => typeof(GuidEditor),
            _ => null),
        new(t => t == typeof(DateTimeOffset),
            _ => typeof(DateTimeOffsetEditor),
            _ => null),
        new(t => t == typeof(DateTime),
            _ => typeof(DateTimeEditor),
            _ => null),
        new(t => t == typeof(bool?),
            _ => typeof(NullableBoolEditor),
            _ => null),
        new(t => t == typeof(bool),
            _ => typeof(BoolEditor),
            _ => false),
        new(t => t == typeof(byte),
            _ => typeof(ByteEditor),
            _ => false),
        new(t => t.IsArray,
            t => typeof(ArrayEditor<>).MakeGenericType(new Type[] { t.GetElementType()! }),
            t => Array.CreateInstance(t.GetElementType()!, 0)),
        new(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(List<>)),
            t => typeof(ListEditor<>).MakeGenericType(new Type[] { t.GenericTypeArguments[0] }),
            t => null),
        new(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(Dictionary<,>)),
            t => typeof(DictionaryEditor<,>).MakeGenericType(new Type[] { t.GenericTypeArguments[0], t.GenericTypeArguments[1] }),
            t => null),
        new(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)),
            t => typeof(KeyValuePairEditor<,>).MakeGenericType(new Type[] { t.GenericTypeArguments[0], t.GenericTypeArguments[1] }),
            t => t.GetConstructor([t.GenericTypeArguments[0], t.GenericTypeArguments[1]])!.Invoke([Editors.First(e => e.CanHandle(t.GenericTypeArguments[0])).InitValue(t.GenericTypeArguments[0]), Editors.First(e => e.CanHandle(t.GenericTypeArguments[1])).InitValue(t.GenericTypeArguments[1])])),
        new(t => t == typeof(DataValue),
            t => typeof(DataValueEditor),
            t => new DataValue("")),
        new(t => t == typeof(int?),
            t => typeof(IntEditor),
            t => null),
        new(t => Nullable.GetUnderlyingType(t) is {},
            t => Nullable.GetUnderlyingType(t) is {} simpleType ? Editors.First(e => e.CanHandle(simpleType)).EditorType(simpleType) : typeof(ObjectEditor<>).MakeGenericType([t]),
            t => null),
        new(t => t.IsAssignableTo(typeof(object)),
            t => typeof(ObjectEditor<>).MakeGenericType(new Type[] { t }),
            t => {
                if (t.IsAbstract)
                {
                    return null;
                }
                if (t.GetConstructors().FirstOrDefault(c => c.GetParameters().Length is 0) is { } parameterLessConstructor)
                {
                    return parameterLessConstructor.Invoke(null);
                }
                try {
                    var someConstructor = t.GetConstructors().First();
                    var parameterTypes = someConstructor.GetParameters().Select(p => p.ParameterType);
                    var defaultValuesForParameters = parameterTypes.Select(t => t.IsValueType ? Activator.CreateInstance(t) : null).ToArray();
                    return someConstructor.Invoke(defaultValuesForParameters);
                }
                catch {
                    return null;
                }
            })
    ];

        public static readonly List<TypeInheritanceLimiter> TypeInheritanceNotIgnored = [
        new([
                typeof(IProductRelevanceModifier),
                typeof(ProductRecommendationRequest),
                typeof(ProductSearchRequest),
                typeof(ProductChangeTriggerConfiguration),
                typeof(ProductQuery),
                typeof(ProductAdministrativeAction)
            ],
            typeof(Filter),
            [
                typeof(IProductFilter),
                typeof(IVariantFilter)
            ]
        ),
        new([
                typeof(IContentRelevanceModifier),
                typeof(ContentRecommendationRequest),
                typeof(ContentSearchRequest),
                typeof(ContentQuery),
                typeof(ContentAdministrativeAction)
            ],
            typeof(Filter),
            [
                typeof(IContentFilter)
            ]
        ),
        new([
                typeof(IBrandRelevanceModifier),
                typeof(BrandRecommendationRequest),
                typeof(BrandQuery),
                typeof(BrandAdministrativeAction)
            ],
            typeof(Filter),
            [
                typeof(IBrandFilter)
            ]
        ),
        new([
                typeof(IProductCategoryRelevanceModifier),
                typeof(ProductCategoryRecommendationRequest),
                typeof(ProductCategorySearchRequest),
                typeof(ProductCategoryQuery),
                typeof(ProductCategoryAdministrativeAction),
            ],
            typeof(Filter),
            [
                typeof(IProductCategoryFilter)
            ]
        ),
        new([
                typeof(IContentCategoryRelevanceModifier),
                typeof(ContentCategoryRecommendationRequest),
                typeof(ContentCategorySearchRequest),
                typeof(ContentCategoryQuery),
                typeof(ContentCategoryAdministrativeAction)
            ],
            typeof(Filter),
            [
                typeof(IContentCategoryFilter)
            ]
        ),
        new([
                typeof(ProductRecommendationRequest),
                typeof(ProductSearchRequest),
                typeof(ProductChangeTriggerConfiguration),
                typeof(ProductQuery),
                typeof(ProductAdministrativeAction)
            ],
            typeof(RelevanceModifier),
            [
                typeof(IProductRelevanceModifier),
                typeof(IVariantRelevanceModifier)
            ]
        ),
        new([
                typeof(ContentRecommendationRequest),
                typeof(ContentSearchRequest),
                typeof(ContentQuery),
                typeof(ContentAdministrativeAction)
            ],
            typeof(RelevanceModifier),
            [
                typeof(IContentRelevanceModifier)
            ]
        ),
        new([
                typeof(BrandRecommendationRequest),
                typeof(BrandQuery),
                typeof(BrandAdministrativeAction)
            ],
            typeof(RelevanceModifier),
            [
                typeof(IBrandRelevanceModifier)
            ]
        ),
        new([
                typeof(ProductCategoryRecommendationRequest),
                typeof(ProductCategorySearchRequest),
                typeof(ProductCategoryQuery),
                typeof(ProductCategoryAdministrativeAction),
            ],
            typeof(RelevanceModifier),
            [
                typeof(IProductCategoryRelevanceModifier)
            ]
        ),
        new([
                typeof(ContentCategoryRecommendationRequest),
                typeof(ContentCategorySearchRequest),
                typeof(ContentCategoryQuery),
                typeof(ContentCategoryAdministrativeAction),
            ],
            typeof(RelevanceModifier),
            [
                typeof(IContentCategoryRelevanceModifier)
            ]
        ),
    ];

    public static readonly List<TypeInheritanceLimiter> TypeInheritanceSupported = [
        new([
                typeof(ProductSearchRequest)
            ],
            typeof(Facet),
            [
                typeof(CategoryFacet),
                typeof(ProductDataStringValueFacet),
                typeof(ProductDataBooleanValueFacet),
                typeof(ProductDataDoubleValueFacet),
                typeof(VariantSpecificationFacet),
                typeof(BrandFacet),
                typeof(ProductAssortmentFacet),
                typeof(PriceRangeFacet),
                typeof(PriceRangesFacet),
                typeof(ProductDataDoubleRangeFacet),
                typeof(ProductDataDoubleRangesFacet),
                typeof(ProductDataObjectFacet),
                typeof(DataObjectStringValueFacet),
                typeof(DataObjectDoubleValueFacet),
                typeof(DataObjectBooleanValueFacet),
                typeof(DataObjectDoubleRangeFacet),
                typeof(DataObjectDoubleRangesFacet),
                typeof(DataObjectFacet),
                typeof(CategoryHierarchyFacet),
                typeof(RecentlyPurchasedFacet),
            ]
        ),
        new([
                typeof(ContentSearchRequest)
            ],
            typeof(Facet),
            [
                typeof(CategoryFacet),
                typeof(ContentDataStringValueFacet),
                typeof(ContentDataBooleanValueFacet),
                typeof(ContentDataDoubleValueFacet),
                typeof(ContentAssortmentFacet),
                typeof(ContentDataDoubleRangeFacet),
                typeof(ContentDataDoubleRangesFacet),
                typeof(DataObjectStringValueFacet),
                typeof(DataObjectDoubleValueFacet),
                typeof(DataObjectBooleanValueFacet),
                typeof(DataObjectDoubleRangeFacet),
                typeof(DataObjectDoubleRangesFacet),
                typeof(DataObjectFacet),
                typeof(ContentDataObjectFacet),
                typeof(CategoryHierarchyFacet),
            ]
        ),
        new([
                typeof(ProductCategorySearchRequest)
            ],
            typeof(Facet),
            [
                typeof(ProductCategoryAssortmentFacet),
                typeof(ProductCategoryDataStringValueFacet),
                typeof(ProductCategoryDataBooleanValueFacet),
                typeof(ProductCategoryDataDoubleValueFacet),
                typeof(ProductCategoryDataDoubleRangeFacet),
                typeof(ProductCategoryDataDoubleRangesFacet),
                typeof(DataObjectStringValueFacet),
                typeof(DataObjectDoubleValueFacet),
                typeof(DataObjectBooleanValueFacet),
                typeof(DataObjectDoubleRangeFacet),
                typeof(DataObjectDoubleRangesFacet),
                typeof(DataObjectFacet),
                typeof(ProductCategoryDataObjectFacet),
            ]
        ),
        new([
                typeof(ContentCategorySearchRequest)
            ],
            typeof(Facet),
            [
                typeof(DataObjectStringValueFacet),
                typeof(DataObjectDoubleValueFacet),
                typeof(DataObjectBooleanValueFacet),
                typeof(DataObjectDoubleRangeFacet),
                typeof(DataObjectDoubleRangesFacet),
                typeof(DataObjectFacet),
            ]
        ),
    ];

    public static readonly List<PropertyInvalidContext> InvalidProperties = [
        new([
                typeof(RecentlyViewedProductsRequest),
                typeof(SortProductsRequest),
                typeof(SortVariantsRequest),
                typeof(PopularProductsRequest),
                typeof(PersonalProductRecommendationRequest),
                typeof(SearchTermBasedProductRecommendationRequest),
                typeof(SimilarProductsRequest),
            ],
            typeof(ProductRecommendationRequestSettings).GetProperty("AllowFillIfNecessaryToReachNumberOfRecommendations")!,
            "Fill is not supported for this recommendation type."
        ),
        new([
                typeof(PopularContentsRequest),
                typeof(PersonalContentRecommendationRequest),
            ],
            typeof(ContentRecommendationRequestSettings).GetProperty("AllowFillIfNecessaryToReachNumberOfRecommendations")!,
            "Fill is not supported for this recommendation type."
        ),
        new([
                typeof(PopularBrandsRecommendationRequest),
                typeof(PersonalBrandRecommendationRequest),
            ],
            typeof(BrandRecommendationRequestSettings).GetProperty("AllowFillIfNecessaryToReachNumberOfRecommendations")!,
            "Fill is not supported for this recommendation type."
        ),
        new([
                typeof(PopularProductCategoriesRecommendationRequest),
            ],
            typeof(ProductCategoryRecommendationRequestSettings).GetProperty("AllowFillIfNecessaryToReachNumberOfRecommendations")!,
            "Fill is not supported for this recommendation type."
        ),
        new([
                typeof(PersonalContentCategoryRecommendationRequest),
                typeof(PopularContentCategoriesRecommendationRequest),
            ],
            typeof(ProductCategoryRecommendationRequestSettings).GetProperty("AllowFillIfNecessaryToReachNumberOfRecommendations")!,
            "Fill is not supported for this recommendation type."
        ),
        new([
                typeof(SortProductsRequest),
            ],
            typeof(ProductRecommendationRequestSettings).GetProperty("RecommendVariant")!,
            "Recommending variants for this recommendation is not supported."
        ),
        new([
                typeof(SortVariantsRequest),
            ],
            typeof(ProductRecommendationRequestSettings).GetProperty("RecommendVariant")!,
            "Recommending variants for this recommendation type is not optional."
        ),
        new([
                typeof(RecentlyViewedProductsRequest),
                typeof(SearchTermBasedProductRecommendationRequest),
                typeof(SimilarProductsRequest),
                typeof(SortProductsRequest),
                typeof(SortVariantsRequest),
            ],
            typeof(ProductRecommendationRequestSettings).GetProperty("AllowReplacingOfRecentlyShownRecommendations")!,
            "Replacing of recently shown products is not supported for this recommendation type."
        ),
        new([
                typeof(RecentlyViewedProductsRequest),
                typeof(SearchTermBasedProductRecommendationRequest),
                typeof(SimilarProductsRequest),
                typeof(SortProductsRequest),
                typeof(SortVariantsRequest),
            ],
            typeof(ProductRecommendationRequestSettings).GetProperty("PrioritizeDiversityBetweenRequests")!,
            "Prioritization of products not recently recommended is not supported for this recommendation type."
        ),
        new([
                typeof(SearchTermBasedProductRecommendationRequest),
                typeof(SortVariantsRequest),
            ],
            typeof(ProductRecommendationRequestSettings).GetProperty("AllowProductsCurrentlyInCart")!,
            "Using this property is not supported for this recommendation type."
        ),
        new([
                typeof(RecentlyViewedProductsRequest),
                typeof(SearchTermBasedProductRecommendationRequest),
                typeof(SimilarProductsRequest),
                typeof(SortProductsRequest),
                typeof(SortVariantsRequest),
            ],
            typeof(ProductRecommendationRequestSettings).GetProperty("PrioritizeResultsNotRecommendedWithinSeconds")!,
            "Prioritization of products not recently recommended is not supported for this recommendation type."
        ),
        new(
            [
                typeof(ProductCategoryRecommendationRequest),
                typeof(ProductCategorySearchRequest),
                typeof(ProductCategoryQuery),
                typeof(ProductCategoryAdministrativeAction),
            ],
            typeof(ProductCategoryIdFilter).GetProperty("EvaluationScope")!,
            "This property is not used when filtering categories. It is only used when this filter is used to filter products."
        ),
        new(
            [
                typeof(ContentCategoryRecommendationRequest),
                typeof(ContentCategorySearchRequest),
                typeof(ContentCategoryQuery),
                typeof(ContentCategoryAdministrativeAction),
            ],
            typeof(ContentCategoryIdFilter).GetProperty("EvaluationScope")!,
            "This property is not used when filtering categories. It is only used when this filter is used to filter content elements."
        ),
    ];

    public static bool CanCreateNoneNullInitValue(Type type) =>
        Editors.FirstOrDefault(editor => editor.CanHandle(type)) is { } editor
        && editor.InitValue(type) is not null;

    public static string Name(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } simpleType)
        {
            return $"{Name(simpleType)}?";
        }

        var name = type.Name.Replace("`1", "").Replace("`2", "");

        if (type.DeclaringType is { } nestedType)
        {
            name = $"{Name(nestedType)}.{name}";
        }

        if (type.GenericTypeArguments is { Length: > 0 } args)
        {
            name += $"<{string.Join(", ", args.Select(t => Name(t)))}>";
        }

        return name;
    }

    public static string PropertyTypeName(PropertyInfo type)
    {
        if (Nullable.GetUnderlyingType(type.PropertyType) is { } simpleType)
        {
            return $"{Name(simpleType)}?";
        }

        if (new NullabilityInfoContext().Create(type).WriteState is NullabilityState.Nullable)
        {
            return $"{Name(type.PropertyType)}?";
        }

        var name = type.PropertyType.Name.Replace("`1", "").Replace("`2", "").Replace("`3", "");

        if (type.PropertyType.DeclaringType is { } nestedType)
        {
            name = $"{Name(nestedType)}.{name}";
        }

        if (type.PropertyType.GenericTypeArguments is { Length: > 0 } args)
        {
            name += $"<{string.Join(", ", args.Select(t => Name(t)))}>";
        }

        return name;
    }

    public static IEnumerable<PropertyInfo> GetProperties(Type type) => type.GetProperties().Where(p => p.SetMethod is not null && p.GetIndexParameters() is { Length: 0 } && p.Name is not "Custom" and not "DatasetId" and not "APIKeySecret");
}

using Bliss.CSharp.Colors;
using Bliss.CSharp.Effects;
using Bliss.CSharp.Graphics;
using Bliss.CSharp.Graphics.Pipelines.Textures;
using Bliss.CSharp.Textures;
using Veldrid;

namespace Bliss.CSharp.Materials;

public class Material : Disposable {
    
    /// <summary>
    /// The graphics device associated with this material, used to manage rendering resources.
    /// </summary>
    public GraphicsDevice GraphicsDevice { get; private set; }
    
    /// <summary>
    /// The effect (shader program) applied to this material.
    /// </summary>
    public Effect Effect { get; private set; }
    
    /// <summary>
    /// Specifies the blend state for rendering, determining how colors are blended on the screen.
    /// </summary>
    public BlendState BlendState { get; private set; }
    
    /// <summary>
    /// An array of texture layouts that defines the material's texture configurations.
    /// </summary>
    public List<SimpleTextureLayout> TextureLayouts { get; private set; }
    
    /// <summary>
    /// A list of floating-point parameters for configuring material properties.
    /// </summary>
    public List<float> Parameters;

    /// <summary>
    /// A dictionary mapping material map types to material map data, used for managing material textures.
    /// </summary>
    private Dictionary<MaterialMapType, MaterialMap> _maps;
    
    /// <summary>
    /// A cache of resource sets mapped by sampler and resource layout, improving efficiency when reusing resources.
    /// </summary>
    private Dictionary<(Sampler, ResourceLayout), ResourceSet> _cachedResourceSets;
    
    private Dictionary<string, MaterialMapType> _mapTypes;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="Material"/> class, configuring it with the specified
    /// graphics device, shader effect, and optional blend state.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device to associate with this material.</param>
    /// <param name="effect">The effect (shader) to apply to the material.</param>
    /// <param name="blendState">The optional blend state to define how this material blends with others during rendering. If not specified, blending is disabled by default.</param>
    public Material(GraphicsDevice graphicsDevice, Effect effect, BlendState? blendState = default) {
        this.GraphicsDevice = graphicsDevice;
        this.Effect = effect;
        this.BlendState = blendState ?? BlendState.Disabled;
        this.TextureLayouts = new List<SimpleTextureLayout>();
        this.Parameters = new List<float>();
        this._maps = new Dictionary<MaterialMapType, MaterialMap> { };
        this._cachedResourceSets = new Dictionary<(Sampler, ResourceLayout), ResourceSet>();
        this._mapTypes = new Dictionary<string, MaterialMapType>();
    }

    /// <summary>
    /// Retrieves the resource set associated with the specified resource layout and material map type.
    /// </summary>
    /// <param name="layout">The resource layout for which the resource set is to be retrieved.</param>
    /// <param name="mapType">The type of the material map.</param>
    /// <returns>The resource set associated with the specified layout and material map type, or null if the texture is not found.</returns>
    public ResourceSet? GetResourceSet(ResourceLayout layout, MaterialMapType mapType) {
        Texture2D? texture = this._maps[mapType].Texture;
        
        if (texture == null) {
            return null;
        }

        Sampler sampler = texture.GetSampler();
        
        if (!this._cachedResourceSets.TryGetValue((sampler, layout), out ResourceSet? resourceSet)) {
            ResourceSet newResourceSet = this.GraphicsDevice.ResourceFactory.CreateResourceSet(new ResourceSetDescription(layout, texture.DeviceTexture, texture.GetSampler()));
            
            this._cachedResourceSets.Add((sampler, layout), newResourceSet);
            return newResourceSet;
        }

        return resourceSet;
    }

    /// <summary>
    /// Get the Materal maps type based on the texture Layout Name
    /// </summary>
    /// <param name="materialMapName"></param>
    /// <returns></returns>
    public MaterialMapType GetMaterialMapType(string materialMapName)
    {
        return this._mapTypes[materialMapName];
    }

    /// <summary>
    /// set the mew dictonary  between the name of texture layout and the material map type
    /// </summary>
    /// <param name="textureLayoutName"></param>
    /// <param name="materialMapType"></param>
    public void SetMaterialMapType(string textureLayoutName, MaterialMapType materialMapType)
    {
        this._mapTypes[textureLayoutName] = materialMapType;
    }

    /// <summary>
    /// Retrieves the material map associated with the specified material map type.
    /// </summary>
    /// <param name="mapType">The type of the material map to retrieve.</param>
    /// <returns>The material map of the specified type.</returns>
    public MaterialMap GetMaterialMap(MaterialMapType mapType) {
        return this._maps[mapType];
    }

    /// <summary>
    /// Sets the material map for the specified material map type.
    /// </summary>
    /// <param name="mapType">The type of the material map to set.</param>
    /// <param name="map">The material map to assign to the specified type.</param>
    public void SetMaterialMap(MaterialMapType mapType, MaterialMap map)
    {
        this._maps[mapType] = map;
    }

    /// <summary>
    /// Retrieves the texture associated with the specified material map type.
    /// </summary>
    /// <param name="mapType">The type of the material map from which to retrieve the texture.</param>
    /// <returns>The texture associated with the specified material map type, or null if none exists.</returns>
    public Texture2D? GetMapTexture(MaterialMapType mapType) {
        return this._maps[mapType].Texture;
    }

    /// <summary>
    /// Sets the texture for the specified material map type.
    /// </summary>
    /// <param name="mapType">The type of material map to set the texture for.</param>
    /// <param name="texture">The texture to associate with the specified material map type.</param>
    public void SetMapTexture(MaterialMapType mapType, Texture2D? texture) {
        this._maps[mapType] = new MaterialMap() {
            Texture = texture,
            Color = this.GetMapColor(mapType),
            Value = this.GetMapValue(mapType)
        };
    }

    /// <summary>
    /// Retrieves the color associated with the specified material map type.
    /// </summary>
    /// <param name="mapType">The type of the material map to retrieve the color for.</param>
    /// <returns>The color of the specified material map type, or null if no color is associated.</returns>
    public Color? GetMapColor(MaterialMapType mapType) {
        return this._maps[mapType].Color;
    }

    /// <summary>
    /// Sets the color for the specified material map type.
    /// </summary>
    /// <param name="mapType">The type of the material map to set the color for.</param>
    /// <param name="color">The color to set for the specified material map type.</param>
    public void SetMapColor(MaterialMapType mapType, Color color) {
        this._maps[mapType] = new MaterialMap() {
            Texture = this.GetMapTexture(mapType),
            Color = color,
            Value = this.GetMapValue(mapType)
        };
    }

    /// <summary>
    /// Retrieves the value associated with the specified material map type.
    /// </summary>
    /// <param name="mapType">The type of the material map to retrieve the value from.</param>
    /// <returns>The numeric value of the specified material map type.</returns>
    public float GetMapValue(MaterialMapType mapType) {
        return this._maps[mapType].Value;
    }

    /// <summary>
    /// Sets the value for the specified material map type.
    /// </summary>
    /// <param name="mapType">The type of the material map to modify.</param>
    /// <param name="value">The value to set for the specified material map type.</param>
    public void SetMapValue(MaterialMapType mapType, float value) {
        this._maps[mapType] = new MaterialMap() {
            Texture = this.GetMapTexture(mapType),
            Color = this.GetMapColor(mapType),
            Value = value
        };
    }

    /// <summary>
    /// Add the texture layout to the end of the layouts
    /// </summary>
    /// <param name="layout"></param>
    public void AddTextureLayout(SimpleTextureLayout layout)
    {
        this.TextureLayouts.Add(layout);
    }
    
    protected override void Dispose(bool disposing) {
        if (disposing) {
            foreach (SimpleTextureLayout textureLayout in this.TextureLayouts) {
                textureLayout.Dispose();
            }

            foreach (ResourceSet resourceSet in this._cachedResourceSets.Values) {
                resourceSet.Dispose();
            }
        }
    }
}
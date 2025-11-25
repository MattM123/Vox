#version 430 core

uniform sampler2DArray texture_sampler;
uniform sampler2DShadow sunlightDepth_sampler;

//The material is a collection of some values that we talked about in the last tutorial,
//some crucial elements to the phong model.
struct Matt {
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
    float shininess; //Shininess is the power the specular light is raised to
};

//The light contains all the values from the light source, how the ambient diffuse and specular values are from the light source.
//This is technically what we were using in the last episode as we were only applying the phong model directly to the light.
struct Light {
    vec3 position;
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
};


//We create the light and the material struct as uniforms.
uniform Light light;
uniform Matt material;

//We still need the view position.
uniform vec3 viewPos;

uniform vec3 playerPos;
uniform int chunkSize;
uniform vec3 targetVertex;
uniform vec3 forwardDir;
uniform int targetTexLayer;

in vec3 fPlayerMin;
in vec3 fPlayerMax;

in vec4 fragPosLightSpace;
flat in float fTexLayer;
flat in int fLighting;
in vec4 fTargetVertex;
in vec2 ftexCoords;
in vec3 fragPos;
in vec3 fnormal;
in vec4 fColor;
in vec3 fforwardDir;
out vec4 color;

//Override specular function
#define SPECULAR_FNC specularBeckmann

//Shader data constructor
#line 1 "shadingData_new.glsl"
#include "lygia\\lighting\\shadingData\\new.glsl"

//Material constructor
#line 1 "material_new.glsl"
#include "lygia\\lighting\\material\\new.glsl"

//Diffuse lighting
#line 1 "diffuse.glsl"
#include "lygia\\lighting\\diffuse.glsl"

#line 1 "fragment.glsl"
float ShadowCalculation(vec4 fragPosLightSpace)
{

    // Perform perspective divide
    vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
    
    // Transform to [0,1] range
    projCoords = projCoords * 0.5 + 0.5;

   // if (projCoords.z > 1.0)
   //     return 0.0; 

    // Bias to reduce shadow acne
    float bias = max(0.005 * (1.0 - dot(normalize(fnormal), normalize(light.position - fragPos))), 0.0005);

    // Sample the shadow map using a shadow sampler (automatically compares with current depth)
    float shadow = texture(sunlightDepth_sampler, vec3(projCoords.xy, projCoords.z));

    return shadow;
}  
float edgeDistance(vec3 p, vec3 minB, vec3 maxB)
{
    // Distance from p to inside of box bounds
    vec3 d = abs((minB + maxB) * 0.5 - p) - (maxB - minB) * 0.5;
    // Negative values = inside box
    return length(max(d, 0.0)) + min(max(d.x, max(d.y, d.z)), 0.0);
}

void main()
{          
    //If air block, discard fragment
    if (fTexLayer == 0)
        discard;

    //=========================================
    // Lighting
    //=========================================

    // Blue component (bits 0-3)           
    float blue = fLighting & 0x000F;

    // Green component (bits 4-7)
    float green = (fLighting & 0x00F0) >> 4;

    // Red component (bits 8-11)
    float red = (fLighting & 0x0F00) >> 8;

    //Normalize color value
    vec3 emissiveColor = vec3(red / 15, green / 15, blue / 15);

    // Sunlight component (bits 12-15)
    float sunlight = (fLighting & 0x0F);
    
   //========================================
   //Shading data 
   //========================================
  //  vec3 frensel = vec3(0.2);                               //~10% reflectivity for everything
    vec3 norm = normalize(fnormal);                         // Surface normal
    vec3 viewDir = normalize(viewPos - fragPos);            // View direction
    vec3 lightDir = normalize(light.position - fragPos);    // Light direction
    vec3 halfVec = normalize(viewDir + lightDir);           // Halfway vector
    vec3 reflection =  reflect(-viewDir, norm);             // Reflicttion

    float specColor = pow(max(dot(viewDir, reflection), 0.0), material.shininess);


    Material mat = Material(
        vec4(material.diffuse, 1.0),   // albedo (RGBA)
        emissiveColor,              // emissive
        fragPos,                    // position
        norm,                       // normal
        0.0,                        // sdf (for raymarching)
        true,                       // valid
        vec3(1.0),                  // ior
        0.5,                        // roughness
        0.0,                        // metallic
        0.5,                        // reflectance
        1.0,                        // ambientOcclusion
        0.0,                        // clearCoat
        0.0                         // clearCoatRoughness
    );
    
    // Populate shading data based on material and modify in place
    ShadingData shadingData = shadingDataNew();

    shadingData.V = viewDir;
    shadingData.L = lightDir;
    shadingData.H = halfVec;
    shadingDataNew(mat, shadingData);
    float NdotL = max(dot(shadingData.N, shadingData.L), 0.0);

    float specular = specColor;
    vec3 diffuse = diffuse(shadingData) * light.diffuse * material.diffuse;
      
    // Basic Lambertian diffuse lighting
    float lightAmount = max(dot(norm, lightDir), 0.0);

    // calculate shadow    
    float shadow = ShadowCalculation(fragPosLightSpace); 
    
    float lightIntensity = 0.5;

    // Apply shadowing
    float shadedLight = lightAmount * (1.0 - shadow) * lightIntensity;

    vec3 ambient = light.ambient * material.ambient;


    vec3 baseLighting = ((lightAmount) * vec3(1,1,1)) * (light.ambient * lightIntensity) ;
    vec3 lightColor = (ambient + diffuse + shadingData.specularColor);
    //If block is emissive, add color to lighting
    if (blue > 0 || red > 0 || green > 0) {
        //Add the emissive color to the blocks lighting value
        lightColor = lightColor + emissiveColor; 
    }

    vec3 result = (baseLighting + lightColor);



    //=========================================
    // Render block target
    //=========================================

    //target bounds checking
    vec3 minBound = fTargetVertex.xyz;
    vec3 maxBound = minBound + vec3(1.0,1.0,1.0);
    vec4 applyTex;
    float gamma = 1.0 / 2.2; 


    // Check if the fragment's position is inside the bounding box
    if ((fragPos.x >= minBound.x && fragPos.x <= maxBound.x &&
        fragPos.y >= minBound.y && fragPos.y <= maxBound.y &&
        fragPos.z >= minBound.z && fragPos.z <= maxBound.z))
    {
        // If inside the bounding box, combine texture with target texture
        vec4 baseTex = texture(texture_sampler, vec3(ftexCoords.xy, fTexLayer));
        vec4 targetOverlay = texture(texture_sampler, vec3(ftexCoords.xy, targetTexLayer));
        applyTex = mix(baseTex, targetOverlay, targetOverlay.a) * vec4(result, 1.0);  
    }
 
    else 
        applyTex = texture(texture_sampler, vec3(ftexCoords.xy, fTexLayer)) * vec4(result, 1.0); 

     color = pow(applyTex, vec4(gamma));

 }

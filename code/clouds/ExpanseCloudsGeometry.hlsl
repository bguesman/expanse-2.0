#ifndef EXPANSE_CLOUDS_GEOMETRY_INCLUDED
#define EXPANSE_CLOUDS_GEOMETRY_INCLUDED

#include "../common/shaders/ExpanseSkyCommon.hlsl"

/* Mirror from ExpanseCommon.cs. */
#define CloudGeometryType_Plane 0
#define CloudGeometryType_Sphere 1
#define CloudGeometryType_BoxVolume 2

/* Mirror from ExpanseCommon.cs. */
#define CloudGeometryDimension_TwoD 2
#define CloudGeometryDimension_ThreeD 3

/* Interface for cloud geometry object. */
interface ICloudGeometry {
  /**
   * @return: whether planet space point p is in bounds of the cloud geometry.
   * */
  bool inBounds(float3 p);

  /**
   * @return: uv coordinate for planet space point p, with tiling factor tile.
   * Assumes coordinate is within the bounds of the geometry.
   * */
  float3 mapCoordinate(float3 p, int3 tile);

  /**
   * @return: starting and ending intersection distances, as, correspondingly,
   * (x, y) if this is a 3D volume. Or, just single intersection, if a 2D
   * volume.
   * */
  float2 intersect(float3 p, float3 d);

  /**
   * @return: same as intersect, unless this is a 2D cloud geometry,
   * in which case something is faked.
   * */
  float2 intersect3D(float3 p, float3 d);

  /**
   * @return: dimension of this cloud volume, used for lighting calculations.
   */
  int dimension();

  /**
   * @return: given a point p, and density attenuation distance and bias,
   * computes the density attenuation factor.
   */
  float densityAttenuation(float3 p, float distance, float bias);

  /**
   * @return: 0-1 value to use in height gradient computations.
   */
  float heightGradient(float3 p);
};








/**
 * @brief: 2D xz-aligned plane.
 */
class CloudPlane : ICloudGeometry {
  float2 xExtent, yExtent, zExtent;
  float height, apparentThickness;

  /* Takes into account apparent thickness to make volumetric shadow queries
   * simpler. */
  bool inBounds(float3 p) {
    return boundsCheck(p.x, xExtent)
      && boundsCheck(p.y, yExtent)
      && boundsCheck(p.z, zExtent);
  }

  /* Disregards y components of p and tile. */
  float3 mapCoordinate(float3 p, int3 tile) {
    float2 minimum = float2(xExtent.x, zExtent.x);
    float2 maximum = float2(xExtent.y, zExtent.y);
    float2 uv = (p.xz - minimum) / (maximum - minimum);
    uv = frac(uv * tile.xz);
    return float3(uv.x, 0, uv.y);
  }

  float2 intersect(float3 p, float3 d) {
    return intersectXZAlignedPlane(p, d, xExtent, zExtent, height);
  }

  /* Useful for self-shadowing hacks. */
  float2 intersect3D(float3 p, float3 d) {
    return intersectAxisAlignedBoxVolume(p, d, xExtent, yExtent, zExtent);
  }

  int dimension() {
    return CloudGeometryDimension_TwoD;
  }

  float densityAttenuation(float3 p, float distance, float bias) {
    float2 origin = float2(dot(xExtent, float2(1, 1))/2, dot(zExtent, float2(1, 1))/2);
    float distFromOrigin = length(origin - p.xz);
    return saturate(exp(-(distFromOrigin-bias)/distance));
  }

  float heightGradient(float3 p) {
    return 0; // We have no meaningful height gradient.
  }
};

CloudPlane CreateCloudPlane(float2 xExtent, float2 zExtent, float height,
  float apparentThickness) {
  CloudPlane c;
  c.xExtent = xExtent;
  c.zExtent = zExtent;
  c.height = height;
  c.apparentThickness = apparentThickness;
  c.yExtent = float2(height-apparentThickness/2, height+apparentThickness/2);
  return c;
}








/**
 * @brief: 3D axis-aligned box.
 */
class CloudBoxVolume : ICloudGeometry {
  float2 xExtent, yExtent, zExtent;

  bool inBounds(float3 p) {
    return boundsCheck(p.x, xExtent)
      && boundsCheck(p.y, yExtent)
      && boundsCheck(p.z, zExtent);
  }

  float3 mapCoordinate(float3 p, int3 tile) {
    // Here we use the x extent as the distance the y coordinate spans,
    // so that the aspect ratio of the noises doesn't get wonky and we
    // don't have to deal with sub-1 grid sizes for our noise.
    float3 minimum = float3(xExtent.x, yExtent.x, zExtent.x);
    float3 maximum = float3(xExtent.y, yExtent.y + (xExtent.y - xExtent.x), zExtent.y);
    float3 uv = (p - minimum) / (maximum - minimum);
    return frac(uv * tile);
  }

  float2 intersect(float3 p, float3 d) {
    return intersectAxisAlignedBoxVolume(p, d, xExtent, yExtent, zExtent);
  }

  float2 intersect3D(float3 p, float3 d) {
    return intersect(p, d);
  }

  int dimension() {
    return CloudGeometryDimension_ThreeD;
  }

  float densityAttenuation(float3 p, float distance, float bias) {
    // Only attenuate based on x-z distance.
    float2 origin = float2(dot(xExtent, float2(1, 1))/2, dot(zExtent, float2(1, 1))/2);
    float distFromOrigin = length(origin - p.xz);
    return saturate(exp(-(distFromOrigin-bias)/distance));
  }

  float heightGradient(float3 p) {
    return (p.y - yExtent.x) / (yExtent.y - yExtent.x);
  }
};

CloudBoxVolume CreateCloudBoxVolume(float2 xExtent, float2 yExtent, float2 zExtent) {
  CloudBoxVolume c;
  c.xExtent = xExtent;
  c.yExtent = yExtent;
  c.zExtent = zExtent;
  return c;
}


#endif // EXPANSE_CLOUDS_GEOMETRY_INCLUDED

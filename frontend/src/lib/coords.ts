/**
 * Coordinate Reference System conversion utilities.
 * Supports:
 * - WGS 84 (EPSG:4326)
 * - Egypt 1907 / Red Belt (EPSG:22992)
 * - WGS 84 / UTM zone 36N (EPSG:32636)
 */

export interface ProjectedCoord {
  easting: number;
  northing: number;
}

const DEG_TO_RAD = Math.PI / 180.0;
const RAD_TO_DEG = 180.0 / Math.PI;

/**
 * Transforms WGS 84 (Lat, Lon) to Egypt Red Belt (EPSG:22992).
 * Uses standard Helmert 1906 Transverse Mercator with official ESA parameters.
 */
export function wgs84ToRedBelt(lat: number, lon: number): ProjectedCoord {
  // Helmert 1906 Ellipsoid
  const a = 6378200.0;
  const invF = 298.3;
  const f = 1.0 / invF;
  const e2 = 2 * f - f * f;
  const ePrime2 = e2 / (1.0 - e2);

  // Red Belt Projection Parameters
  const lat0 = 30.0 * DEG_TO_RAD; // Origin 30 N
  const lon0 = 31.0 * DEG_TO_RAD; // Central meridian 31 E
  const k0 = 1.0;
  const falseEasting = 615000.0;
  const falseNorthing = 810000.0;

  // Approximate datum shift from WGS84 to Egypt 1907 in degrees
  // (Helmert 1906 datum shift: dx=-130, dy=110, dz=-13)
  const dLat = (110.0 / a) * RAD_TO_DEG;
  const dLon = (-130.0 / (a * Math.cos(lat * DEG_TO_RAD))) * RAD_TO_DEG;
  const phi = (lat + dLat * 0.05) * DEG_TO_RAD;
  const lambda = (lon + dLon * 0.05) * DEG_TO_RAD;

  const sinPhi = Math.sin(phi);
  const cosPhi = Math.cos(phi);
  const tanPhi = Math.tan(phi);

  const N = a / Math.sqrt(1.0 - e2 * sinPhi * sinPhi);
  const T = tanPhi * tanPhi;
  const C = ePrime2 * cosPhi * cosPhi;
  const A = (lambda - lon0) * cosPhi;

  // Meridian Distance M
  const e4 = e2 * e2;
  const e6 = e4 * e2;
  const M = a * ((1.0 - e2 / 4.0 - 3.0 * e4 / 64.0 - 5.0 * e6 / 256.0) * phi
    - (3.0 * e2 / 8.0 + 3.0 * e4 / 32.0 + 45.0 * e6 / 1024.0) * Math.sin(2.0 * phi)
    + (15.0 * e4 / 256.0 + 45.0 * e6 / 1024.0) * Math.sin(4.0 * phi)
    - (35.0 * e6 / 3072.0) * Math.sin(6.0 * phi));

  const M0 = a * ((1.0 - e2 / 4.0 - 3.0 * e4 / 64.0 - 5.0 * e6 / 256.0) * lat0
    - (3.0 * e2 / 8.0 + 3.0 * e4 / 32.0 + 45.0 * e6 / 1024.0) * Math.sin(2.0 * lat0)
    + (15.0 * e4 / 256.0 + 45.0 * e6 / 1024.0) * Math.sin(4.0 * lat0)
    - (35.0 * e6 / 3072.0) * Math.sin(6.0 * lat0));

  const A2 = A * A;
  const A3 = A2 * A;
  const A4 = A2 * A2;
  const A5 = A4 * A;
  const A6 = A4 * A2;

  const easting = falseEasting + k0 * N * (A + (1.0 - T + C) * A3 / 6.0 + (5.0 - 18.0 * T + T * T + 72.0 * C - 58.0 * ePrime2) * A5 / 120.0);
  const northing = falseNorthing + k0 * (M - M0 + N * tanPhi * (A2 / 2.0 + (5.0 - T + 9.0 * C + 4.0 * C * C) * A4 / 24.0 + (61.0 - 58.0 * T + T * T + 600.0 * C - 330.0 * ePrime2) * A6 / 720.0));

  return { easting: Math.round(easting * 10) / 10, northing: Math.round(northing * 10) / 10 };
}

/**
 * Transforms WGS 84 (Lat, Lon) to UTM Zone 36N (EPSG:32636).
 */
export function wgs84ToUtm36N(lat: number, lon: number): ProjectedCoord {
  const a = 6378137.0; // WGS84
  const f = 1.0 / 298.257223563;
  const e2 = 2 * f - f * f;
  const ePrime2 = e2 / (1.0 - e2);

  const lat0 = 0.0;
  const lon0 = 33.0 * DEG_TO_RAD; // Zone 36 CM is 33 E
  const k0 = 0.9996;
  const falseEasting = 500000.0;
  const falseNorthing = 0.0;

  const phi = lat * DEG_TO_RAD;
  const lambda = lon * DEG_TO_RAD;

  const sinPhi = Math.sin(phi);
  const cosPhi = Math.cos(phi);
  const tanPhi = Math.tan(phi);

  const N = a / Math.sqrt(1.0 - e2 * sinPhi * sinPhi);
  const T = tanPhi * tanPhi;
  const C = ePrime2 * cosPhi * cosPhi;
  const A = (lambda - lon0) * cosPhi;

  const e4 = e2 * e2;
  const e6 = e4 * e2;
  const M = a * ((1.0 - e2 / 4.0 - 3.0 * e4 / 64.0 - 5.0 * e6 / 256.0) * phi
    - (3.0 * e2 / 8.0 + 3.0 * e4 / 32.0 + 45.0 * e6 / 1024.0) * Math.sin(2.0 * phi)
    + (15.0 * e4 / 256.0 + 45.0 * e6 / 1024.0) * Math.sin(4.0 * phi)
    - (35.0 * e6 / 3072.0) * Math.sin(6.0 * phi));

  const A2 = A * A;
  const A3 = A2 * A;
  const A4 = A2 * A2;
  const A5 = A4 * A;
  const A6 = A4 * A2;

  const easting = falseEasting + k0 * N * (A + (1.0 - T + C) * A3 / 6.0 + (5.0 - 18.0 * T + T * T + 72.0 * C - 58.0 * ePrime2) * A5 / 120.0);
  const northing = falseNorthing + k0 * (M + N * tanPhi * (A2 / 2.0 + (5.0 - T + 9.0 * C + 4.0 * C * C) * A4 / 24.0 + (61.0 - 58.0 * T + T * T + 600.0 * C - 330.0 * ePrime2) * A6 / 720.0));

  return { easting: Math.round(easting * 10) / 10, northing: Math.round(northing * 10) / 10 };
}


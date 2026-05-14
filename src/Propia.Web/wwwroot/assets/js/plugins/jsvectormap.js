document.addEventListener('DOMContentLoaded', () => {

    // -----------------------------
    // Common markers
    // -----------------------------
    const commonMarkers = [
        { name: "Russia", coords: [61.524, 105.3188] },
        { name: "Greenland", coords: [71.7069, -42.6043] },
        { name: "Egypt", coords: [26.8206, 30.8025] },
        { name: "Brazil", coords: [-14.2350, -51.9253] },
        { name: "China", coords: [35.8617, 104.1954] },
        { name: "United States", coords: [37.0902, -95.7129] },
        { name: "Ukraine", coords: [48.379433, 31.16558] },
    ];

    // -----------------------------
    // Common options
    // -----------------------------
    const defaultOptions = {
        regionStyle: { initial: { fill: 'rgba(var(--bs-dark-rgb), 0.12)' } },
        zoomButtons: false,
        zoomOnScroll: false,
        zoomMax: 10,
        zoomMin: 1
    };

    // -----------------------------
    // Map configurations
    // -----------------------------
    const mapConfigs = [
        {
            selector: "#jsVectorMap_World",
            options: {
                map: "world",
                markers: commonMarkers.map(marker => ({
                    ...marker,
                    style: {
                        initial: {
                            fill: marker.name === "Russia" ? 'var(--bs-danger)' :
                                  marker.name === "Greenland" ? 'var(--bs-success)' :
                                  marker.name === "Egypt" ? 'var(--bs-secondary)' :
                                  marker.name === "Brazil" ? 'var(--bs-dark)' :
                                  marker.name === "China" ? 'var(--bs-secondary)' :
                                  marker.name === "United States" ? 'var(--bs-primary)' :
                                  undefined,
                            stroke: marker.name === "United States" ? undefined :
                                    marker.name === "Russia" ? 'var(--bs-danger)' :
                                    marker.name === "Greenland" ? 'var(--bs-success)' :
                                    marker.name === "Egypt" ? 'var(--bs-secondary)' :
                                    marker.name === "Brazil" ? 'var(--bs-dark)' :
                                    marker.name === "China" ? 'var(--bs-secondary)' :
                                    undefined
                        }
                    }
                })),
                markerStyle: {
                    initial: { r: 3, fill: 'var(--bs-primary)', stroke: 'var(--bs-primary)', strokeWidth: 6, strokeOpacity: 0.25, fillOpacity: 1 },
                    hover: { fill: 'var(--bs-primary)' }
                }
            }
        },
        {
            selector: "#jsVectorMap_Marker",
            options: {
                map: "world",
                markers: commonMarkers,
                markerStyle: {
                    initial: { image: 'assets/libs/jsvectormap/pin.png', width: 18, height: 18 },
                    hover: { fill: 'var(--bs-primary)' }
                }
            }
        },
        {
            selector: "#jsVectorMap_Lines",
            options: {
                map: "world",
                markers: commonMarkers.map(marker => ({
                    ...marker,
                    style: {
                        initial: {
                            fill: ['Russia','Greenland','United States'].includes(marker.name) ? 'var(--bs-primary)' :
                                  ['Egypt','Brazil','China','Ukraine'].includes(marker.name) ? 'var(--bs-secondary)' :
                                  undefined,
                            stroke: ['Egypt','Brazil','China','Ukraine'].includes(marker.name) ? 'var(--bs-secondary)' : undefined
                        }
                    }
                })),
                lines: [
                    { from: 'Russia', to: 'Greenland' },
                    { from: 'Russia', to: 'United States' },
                    { from: 'Brazil', to: 'Ukraine' },
                    { from: 'Brazil', to: 'Egypt' },
                    { from: 'Brazil', to: 'China' }
                ],
                markerStyle: {
                    initial: { r: 3, fill: 'var(--bs-primary)', stroke: 'var(--bs-primary)', strokeWidth: 6, strokeOpacity: 0.25, fillOpacity: 1 },
                    hover: { fill: 'var(--bs-primary)' }
                },
                lineStyle: { stroke: 'var(--bs-body-color)', strokeDasharray: '6 3 6', animation: true, curvature: -0.5 }
            }
        },
        {
            selector: "#jsVectorMap_Region",
            options: {
                map: "world",
                regionStyle: {
                    initial: { fill: 'rgba(var(--bs-dark-rgb), 0.12)' },
                    hover: { fill: 'var(--bs-primary)' },
                    selected: { fill: 'var(--bs-danger)' },
                    selectedHover: { fill: 'var(--bs-danger)' }
                },
                selectedRegions: ['BR', 'RU']
            }
        }
    ];

    // -----------------------------
    // Initialize all maps
    // -----------------------------
    mapConfigs.forEach(({ selector, options }) => {
        const el = document.querySelector(selector);
        if (el) new jsVectorMap({ selector, ...defaultOptions, ...options });
    });

});
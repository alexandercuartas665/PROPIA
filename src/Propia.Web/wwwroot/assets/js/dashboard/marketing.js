var revChartConfig = {
	series: [{ data: [40, 60, 50, 70, 50, 67, 54] }],
	chart: {
		type: "bar",
		height: 90,
		width: "100%",
		toolbar: { show: false },
		sparkline: { enabled: true }
	},
	colors: ["var(--bs-primary)"],
	stroke: {
		width: 0,
		curve: "smooth",
		dashArray: [0]
	},
	fill: {
		type: ["gradient"],
		gradient: {
			shade: 'light',
			type: "vertical",
			shadeIntensity: 0.1,
			gradientToColors: ["var(--bs-primary)"],
			inverseColors: false,
			opacityFrom: 0.5,
			opacityTo: 0.00,
			stops: [20, 0]
		}
	},
	tooltip: {
		enabled: false
	}

};
const revChart = document.querySelector("#revChart");
if (revChart) {
	var chartInit = new ApexCharts(revChart, revChartConfig);
	chartInit.render();
}



var aovChartConfig = {
	series: [{ data: [44, 46, 46, 45, 47, 46, 46] }],
	chart: {
		type: "area",
		height: 90,
		width: "100%",
		toolbar: { show: false },
		sparkline: { enabled: true }
	},
	colors: ["var(--bs-dark)"],
	stroke: {
		width: 2,
		curve: "smooth",
		dashArray: [5]
	},
	fill: {
		type: ["gradient"],
		gradient: {
			shade: 'light',
			type: "vertical",
			shadeIntensity: 0.1,
			gradientToColors: ["var(--bs-dark)"],
			inverseColors: false,
			opacityFrom: 0.05,
			opacityTo: 0.01,
			stops: [20, 100]
		}
	},
	tooltip: {
		enabled: false
	}

};
const aovChart = document.querySelector("#aovChart");
if (aovChart) {
	var chartInit = new ApexCharts(aovChart, aovChartConfig);
	chartInit.render();
}



var purchaseChartConfig = {
	series: [{
		data: [180, 210, 240, 200, 260, 300, 310]
	}],
	chart: {
		type: "bar",
		height: 90,
		sparkline: { enabled: true },
		toolbar: { show: false }
	},
	plotOptions: {
		bar: { columnWidth: "60%" }
	},
	colors: ["rgba(var(--bs-info-rgb),0.6)"],
	stroke: {
		width: 0,
		curve: "smooth"
	},
	tooltip: {
		enabled: false
	}

};
const purchaseChart = document.querySelector("#purchaseChart");
if (purchaseChart) {
	var chartInit = new ApexCharts(purchaseChart, purchaseChartConfig);
	chartInit.render();
}



var growthChartConfig = {
	series: [{ data: [50, 20, 70, 20, 60, 20, 95] }],
	chart: {
		type: "area",
		height: 90,
		width: "100%",
		toolbar: { show: false },
		sparkline: { enabled: true }
	},
	colors: ["var(--bs-success)"],
	stroke: {
		width: 2,
		curve: "smooth",
		dashArray: [5]
	},
	fill: {
		type: ["gradient"],
		gradient: {
			shade: 'light',
			type: "vertical",
			shadeIntensity: 0.1,
			gradientToColors: ["var(--bs-success)"],
			inverseColors: false,
			opacityFrom: 0.08,
			opacityTo: 0.01,
			stops: [20, 100]
		}
	},
	tooltip: {
		enabled: false
	}

};
const growthChart = document.querySelector("#growthChart");
if (growthChart) {
	var chartInit = new ApexCharts(growthChart, growthChartConfig);
	chartInit.render();
}



var adsTrendChartConfig = {
	series: [
		{
			name: 'Clicks',
			data: [1500, 4000, 4200, 5500, 4000, 5200, 7800, 6200, 5000, 4200, 7000, 7950]
		}
	],
	chart: {
		height: 320,
		type: 'area',
		zoom: { enabled: false },
		toolbar: { show: false },
	},
	colors: [
		"var(--bs-primary)",
		"var(--bs-danger)"
	],
	fill: {
		type: ["gradient"],
		gradient: {
			shade: 'light',
			type: "vertical",
			shadeIntensity: 0.1,
			gradientToColors: ["var(--bs-primary)"],
			inverseColors: false,
			opacityFrom: 0.08,
			opacityTo: 0.01,
			stops: [20, 100]
		}
	},
	dataLabels: { enabled: false },
	stroke: {
		width: [2],
		curve: 'stepline',
		dashArray: [5]
	},
	markers: {
		size: 0,
		colors: ['#FFFFFF'],
		strokeColors: 'var(--bs-info)',
		strokeWidth: 2,
		hover: {
			size: 6
		}
	},
	yaxis: {
		min: 0,
		max: 8000,
		tickAmount: 5,
		labels: {
			style: {
				colors: 'var(--bs-body-color)',
				fontSize: '13px',
				fontWeight: '500',
				fontFamily: 'var(--bs-body-font-family)'
			}
		}
	},
	xaxis: {
		categories: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
		axisBorder: { color: 'var(--bs-border-color)' },
		axisTicks: { show: false },
		labels: {
			style: {
				colors: 'var(--bs-body-color)',
				fontSize: '13px',
				fontWeight: '500',
				fontFamily: 'var(--bs-body-font-family)'
			}
		}
	},
	grid: {
		borderColor: 'var(--bs-border-color)',
		strokeDashArray: 5,
		xaxis: { lines: { show: false } },
		yaxis: { lines: { show: true } }
	},
	legend: {
		show: true,
		position: 'bottom',
		horizontalAlign: 'center',
		markers: {
			size: 5,
			shape: 'circle',
			radius: 10,
			width: 10,
			height: 10,
		},
		labels: {
			colors: 'var(--bs-heading-color)',
			fontFamily: 'var(--bs-body-font-family)',
			fontSize: '13px',
		}
	}
};
const adsTrendChart = document.querySelector("#adsTrendChart");
if (adsTrendChart) {
	var chartInit = new ApexCharts(adsTrendChart, adsTrendChartConfig);
	chartInit.render();
}



var leadFunnelChartConfig = {
	chart: {
		type: "bar",
		height: 440,
		toolbar: {
			show: false
		}
	},
	plotOptions: {
		bar: {
			horizontal: true,
			distributed: true,
			barHeight: "65%",
			borderRadius: 3
		}
	},
	colors: [
		"var(--bs-primary)",
		"var(--bs-primary)",
		"var(--bs-primary)",
		"var(--bs-primary)",
		"var(--bs-primary)",
		"var(--bs-primary)"
	],
	dataLabels: {
		enabled: true,
		formatter: function (val, opts) {
			let base = [8140, 5720, 4860, 3640, 2220, 1910];
			let percentage = ((val / base) * 100).toFixed(1);
			return val + " (" + percentage + "%)";
		},
		style: {
			fontSize: "13px",
			fontWeight: 600
		}
	},
	series: [{
		name: "Leads",
		data: [8140, 5720, 4860, 3640, 2220, 1910],
	}],
	xaxis: {
		categories: [
			"New Leads",
			"Contacted",
			"Qualified",
			"In Progress",
			"Closed Won",
			"Closed Lost"
		],
		labels: {
			show: true,
			style: {
				fontSize: "13px",
				fontWeight: 500,
				colors: "var(--bs-body-color)"
			}
		},
		axisBorder: {
			show: true,
			color: "var(--bs-border-color)"
		},
		axisTicks: {
			show: true,
			color: "var(--bs-border-color)"
		}
	},
	yaxis: {
		labels: {
			style: {
				fontSize: "14px",
				fontWeight: 500,
				colors: "var(--bs-body-color)"
			}
		}
	},
	grid: {
		borderColor: "#e6e6e6",
		strokeDashArray: 4
	},
	legend: {
		show: false
	}
};
const leadFunnelChart = document.querySelector("#leadFunnelChart");
if (leadFunnelChart) {
	var chartInit = new ApexCharts(leadFunnelChart, leadFunnelChartConfig);
	chartInit.render();
}
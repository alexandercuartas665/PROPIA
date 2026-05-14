const dealsStageConfig = {
	chart: {
		type: 'bar',
		height: 400,
		stacked: true,
		toolbar: { show: false },
		background: 'transparent',
		animations: {
			enabled: true,
			easing: 'easeinout',
			speed: 900,
			animateGradually: { enabled: true, delay: 150 }
		},
	},
	plotOptions: {
		bar: {
			horizontal: false,
			columnWidth: '42%',
			borderRadius: 4,
			borderRadiusApplication: 'end',
		}
	},
	series: [
		{
			name: 'Lost Deals',
			data: [120, 80, 60, 50, 40, 30],
		},
		{
			name: 'Won Deals',
			data: [200, 160, 140, 180, 190, 250],
		}
	],
	colors: ['var(--bs-primary)', 'rgba(var(--bs-primary-rgb),0.1)'],
	dataLabels: {
		enabled: true,
		offsetY: -5,
		style: {
			fontSize: '11px',
			colors: ['#111'],
			fontFamily: 'var(--bs-body-font-family)',
			fontWeight: '500',
		},
		background: {
			enabled: true,
			borderRadius: 2,
			foreColor: '#fff',
			opacity: 1,
			borderWidth: 0
		},
	},
	xaxis: {
		categories: [
			'New',
			'Contacted',
			'Qualified',
			'Proposal Sent',
			'Negotiation',
			'Won / Lost'
		],
		axisBorder: { show: false },
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
	yaxis: {
		min: 0,
		max: 350,
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
	legend: {
		position: 'bottom',
		horizontalAlign: 'center',
		fontSize: '13px',
		fontWeight: 500,
		labels: {
			colors: 'var(--bs-body-color)'
		},
		markers: {
			width: 12, height: 12
		}
	},
	grid: {
		borderColor: 'var(--bs-border-color)',
		strokeDashArray: 5,
		xaxis: { lines: { show: false } },
		yaxis: { lines: { show: true } }
	},
	tooltip: {
		theme: 'dark',
		style: { fontSize: '13px' },
		y: { formatter: (val) => val + ' deals' }
	}
};
const dealsStageChart = document.querySelector("#dealsStageChart");
if (typeof dealsStageChart !== undefined && dealsStageChart !== null) {
	const chartInit = new ApexCharts(dealsStageChart, dealsStageConfig);
	chartInit.render();
}



const leadSourceChartConfig = {
	series: [40, 25, 20, 10, 5],
	chart: {
		type: 'donut',
		height: 380,
		toolbar: {
			show: false
		}
	},
	labels: [
		'Website Form',
		'Email Campaign',
		'Social Media',
		'Referral',
		'Other'
	],
	colors: [
		'rgba(var(--bs-primary-rgb), 1)',
		'rgba(var(--bs-primary-rgb), 0.7)',
		'rgba(var(--bs-primary-rgb), 0.5)',
		'rgba(var(--bs-primary-rgb), 0.2)',
		'rgba(var(--bs-primary-rgb), 0.09)'
	],
	legend: {
		position: 'bottom',
		horizontalAlign: 'center',
		fontSize: '14px',
		labels: {
			colors: 'var(--bs-body-color)'
		}
	},
	dataLabels: {
		enabled: true,
		formatter: function (val) {
			return val.toFixed(0) + "%";
		},
		style: {
			fontSize: '13px',
			colors: ['#fff']
		},
		dropShadow: {
			enabled: false
		}
	},
	stroke: {
		width: 0
	},
	tooltip: {
		y: {
			formatter: function (val) {
				return val + "% of total leads";
			}
		}
	},
	plotOptions: {
		pie: {
			donut: {
				size: '55%',
				labels: {
					show: true,
					total: {
						show: true,
						label: 'Total Leads',
						fontSize: '14px',
						color: 'var(--bs-body-color)',
						formatter: function (w) {
							return '100%';
						}
					}
				}
			}
		}
	}
}
const leadSourceChart = document.querySelector("#leadSourceChart");
if (typeof leadSourceChart !== undefined && leadSourceChart !== null) {
	const chartInit = new ApexCharts(leadSourceChart, leadSourceChartConfig);
	chartInit.render();
}


const ConversionFunnelConfig = {
	series: [{
		name: 'Leads',
		data: [4258, 3120, 2254, 1840, 1420, 800]
	}],
	chart: {
		type: 'bar',
		height: 370,
		toolbar: {
			show: false
		}
	},
	plotOptions: {
		bar: {
			horizontal: true,
			barHeight: '80%',
			isFunnel: true
		}
	},
	dataLabels: {
		enabled: true,
		formatter: (val, opt) => {
			const stage = ['New Leads', 'Contacted', 'Qualified', 'Proposal Sent', 'Negotiation', 'Won'][opt.dataPointIndex];
			return `${stage}: ${val}`;
		},
		style: { fontSize: '13px' }
	},
	xaxis: {
		categories: ['New Leads', 'Contacted', 'Qualified', 'Proposal Sent', 'Negotiation', 'Won']
	},
	colors: ['#5955D1', '#7C3AED', '#8B5CF6', '#A78BFA', '#C4B5FD', '#EDE9FE']
}
const ConversionFunnel = document.querySelector("#ConversionFunnel");
if (typeof ConversionFunnel !== undefined && ConversionFunnel !== null) {
	const chartInit = new ApexCharts(ConversionFunnel, ConversionFunnelConfig);
	chartInit.render();
}

const OpportunityChartConfig = {
	chart: {
		type: 'bar',
		height: 420,
		stacked: false,
		toolbar: {
			show: false
		},
		background: 'transparent',
		animations: {
			enabled: true,
			easing: 'easeinout',
			speed: 900,
			animateGradually: { enabled: true, delay: 150 }
		},
	},
	plotOptions: {
		bar: {
			horizontal: true,
			barHeight: '70%',
			borderRadius: 6,
		}
	},
	series: [
		{
			name: 'Deal Value ($)',
			data: [25000, 40000, 65000, 45000, 90000],
		}
	],
	colors: ['var(--bs-primary)'],
	dataLabels: {
		enabled: true,
		style: {
			fontSize: '13px',
			colors: ['#fff'],
			fontWeight: 500,
			fontFamily: 'var(--bs-body-font-family)'
		},
		formatter: (val) => "$" + val.toLocaleString()
	},
	xaxis: {
		categories: [
			'Prospecting',
			'Qualification',
			'Proposal Sent',
			'Negotiation',
			'Closed Won'
		],
		axisBorder: { show: false },
		axisTicks: { show: false },
		labels: {
			style: {
				colors: 'var(--bs-body-color)',
				fontSize: '13px',
				fontWeight: 500,
				fontFamily: 'var(--bs-body-font-family)'
			},
			formatter: (val) => "$" + val.toLocaleString()
		}
	},
	yaxis: {
		labels: {
			style: {
				colors: 'var(--bs-body-color)',
				fontSize: '13px',
				fontWeight: 500,
				fontFamily: 'var(--bs-body-font-family)'
			},
		}
	},
	legend: {
		show: false 
	},
	grid: {
		show: false,
		borderColor: 'rgba(255,255,255,0.1)',
		strokeDashArray: 5,
		padding: { top: 5, bottom: 5, left: 10, right: 5 }
	},
	tooltip: {
		theme: 'dark',
		style: { fontSize: '13px' },
		y: { formatter: (val) => "$" + val.toLocaleString() }
	}
};
const OpportunityChart = document.querySelector("#OpportunityChart");
if (typeof OpportunityChart !== undefined && OpportunityChart !== null) {
	const chartInit = new ApexCharts(OpportunityChart, OpportunityChartConfig);
	chartInit.render();
}


const LostOpportunityChartConfig = {
	chart: {
		type: 'pie',
		height: 380,
		background: 'transparent',
		toolbar: { show: false },
		animations: {
			enabled: true,
			easing: 'easeinout',
			speed: 900,
			animateGradually: { enabled: true, delay: 150 }
		},
	},
	series: [35, 25, 20, 15, 5],
	labels: [
		'High Pricing',
		'No Response',
		'Competitor Advantage',
		'Delayed Decision',
		'Other'
	],
	colors: [
		'rgba(var(--bs-primary-rgb), 1)',
		'rgba(var(--bs-primary-rgb), 0.7)',
		'rgba(var(--bs-primary-rgb), 0.5)',
		'rgba(var(--bs-primary-rgb), 0.2)',
		'rgba(var(--bs-primary-rgb), 0.09)'
	],
	dataLabels: {
		enabled: true,
		style: {
			fontSize: '13px',
			fontWeight: 'bold'
		},
		formatter: (val) => val.toFixed(0) + '%'
	},
	legend: {
		position: 'bottom',
		horizontalAlign: 'center',
		fontSize: '13px',
		fontWeight: 500,
		labels: {
			colors: 'var(--bs-body-color)'
		},
		markers: {
			width: 12, height: 12
		}
	},
	stroke: {
		show: true,
		width: 0,
		colors: ['transparent']
	},
	grid: {
		padding: {
			top: 5,
			bottom: 5,
			left: 5,
			right: 5
		}
	},
	tooltip: {
		theme: 'dark',
		style: {
			colors: 'var(--bs-body-color)',
			fontSize: '13px',
			fontWeight: 500,
			fontFamily: 'var(--bs-body-font-family)'
		},
		y: {
			formatter: (val) => val + '%'
		}
	},
	responsive: [{
		breakpoint: 480,
		options: {
			chart: {
				height: 320
			},
			legend: {
				position: 'bottom'
			}
		}
	}]
};
const LostOpportunityChart = document.querySelector("#LostOpportunityChart");
if (typeof LostOpportunityChart !== undefined && LostOpportunityChart !== null) {
	const chartInit = new ApexCharts(LostOpportunityChart, LostOpportunityChartConfig);
	chartInit.render();
}
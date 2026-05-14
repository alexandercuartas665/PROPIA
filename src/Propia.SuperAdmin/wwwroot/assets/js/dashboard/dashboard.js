if ($('#dt_NewCustomers').length) {
	const dt_NewCustomers = $('#dt_NewCustomers').DataTable({
		searching: true,
		pageLength: 6,
		select: false,
		lengthChange: false,
		info: true,
		paging: true,
		language: {
			search: "",
			searchPlaceholder: 'Search',
			paginate: {
				previous: "<i class='fi fi-rr-angle-left'></i>",
				next: "<i class='fi fi-rr-angle-right'></i>",
				first: "<i class='fi fi-rr-angle-double-left'></i>",
				last: "<i class='fi fi-rr-angle-double-right'></i>"
			},
		},
		initComplete: function () {
			var dtSearch = $('#dt_NewCustomers_wrapper .dt-search').detach();
			$('#dt_NewCustomers_Search').append(dtSearch);
			$('#dt_NewCustomers_Search .dt-search').prepend('<i class="fi fi-rr-search"></i>');
			$('#dt_NewCustomers_Search .dt-search label').remove();
			$('#dt_NewCustomers_wrapper > .row.mt-2.justify-content-between').first().remove();
		},
		columnDefs: [{
			targets: [0],
			orderable: false,
		}]
	});
}


if ($('#dt_CustomerList').length) {
	const dt_CustomerList = $('#dt_CustomerList').DataTable({
		searching: true,
		pageLength: 12,
		select: false,
		lengthChange: false,
		info: true,
		paging: true,
		language: {
			search: "",
			searchPlaceholder: 'Search',
			paginate: {
				previous: "<i class='fi fi-rr-angle-left'></i>",
				next: "<i class='fi fi-rr-angle-right'></i>",
				first: "<i class='fi fi-rr-angle-double-left'></i>",
				last: "<i class='fi fi-rr-angle-double-right'></i>"
			},
		},
		initComplete: function() {
			var dtSearch = $('#dt_CustomerList_wrapper .dt-search').detach();
			$('#dt_CustomerList_Search').append(dtSearch);
			$('#dt_CustomerList_Search .dt-search').prepend('<i class="fi fi-rr-search"></i>');
			$('#dt_CustomerList_Search .dt-search label').remove();
			$('#dt_CustomerList_wrapper > .row.mt-2.justify-content-between').first().remove();
		},
		columnDefs: [{ 
			targets: [0],
			orderable: false,
		}]
	});
}


const chartTrafficSourcesConfig = {
	series: [
		{
			name: 'Organic Search',
			data: [41.5]
		},
		{
			name: 'Direct Traffic',
			data: [27]
		},
		{
			name: 'Referral Traffic',
			data: [18]
		},
		{
			name: 'Social Media',
			data: [10.3]
		},
		{
			name: 'Email Traffic',
			data: [3.2]
		}
	],
	chart: {
		type: 'bar',
		height: 95,
		stacked: true,
		stackType: '100%',
		toolbar: {
			show: false
		},
		animations: {
			enabled: true
		},
	},
	plotOptions: {
		bar: {
			horizontal: true,
			barHeight: '100%',
			borderRadius: 0
		}
	},
	dataLabels: {
		enabled: false
	},
	stroke: {
		width: 1,
		colors: ['#ffffff'],
	},
	xaxis: {
		labels: {
			show: false
		},
		axisBorder: {
			show: false
		},
		axisTicks: {
			show: false
		}
	},
	yaxis: {
		labels: {
			show: false
		}
	},
	grid: {
		show: false,
		padding: {
			top: -15,
			bottom: -15,
			left: -15,
			right: 0
		}
	},
	legend: {
		show: false
	},
	fill: {
		opacity: 1,
		colors: [
			'rgba(var(--bs-primary-rgb), 0.1)',
			'rgba(var(--bs-primary-rgb), 0.25)',
			'rgba(var(--bs-primary-rgb), 0.50)',
			'rgba(var(--bs-primary-rgb), 0.75)',
			'rgba(var(--bs-primary-rgb), 1)'
		]
	},
	tooltip: {
		enabled: true,
		y: {
			formatter: function (val) {
				return val + "%";
			}
		}
	}
};
const chartTrafficSources = document.querySelector("#chartTrafficSources");
if (typeof chartTrafficSources !== undefined && chartTrafficSources !== null) {
	const chartInit = new ApexCharts(chartTrafficSources, chartTrafficSourcesConfig);
	chartInit.render();
}


const chartOrderByTimeConfig = {
	series: [
		{
			name: '8am',
			data: [10, 12, 8, 15, 5, 7, 9]
		},
		{
			name: '10am',
			data: [20, 25, 18, 30, 12, 15, 10]
		},
		{
			name: '12pm',
			data: [30, 28, 22, 50, 25, 20, 18]
		},
		{
			name: '2pm',
			data: [15, 18, 12, 22, 28, 25, 14]
		},
		{
			name: '4pm',
			data: [10, 14, 9, 18, 20, 15, 12]
		}
	],
	chart: {
		height: 250,
		type: 'heatmap',
		toolbar: {
			show: false
		}
	},
	stroke: {
        width: 2,
        colors: ["var(--bs-body-bg)"]
    },
	dataLabels: {
		enabled: false
	},
	plotOptions: {
		heatmap: {
			shadeIntensity: 0.95,
			radius: 6,
			distributed: false,
			colorScale: {
				ranges: [
					{ from: 0, to: 10, color: "#E0E7FF" },
					{ from: 11, to: 25, color: "#A5B4FC" },
					{ from: 26, to: 50, color: "#6366F1" }
				]
			}
		}
	},
	grid: {
		show: false,
	},
	yaxis: {
		min: 0,
		max: 500,
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
		categories: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
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
	legend: {
		show: false,
	}
}
const chartOrderByTime = document.querySelector("#chartOrderByTime");
if (typeof chartOrderByTime !== undefined && chartOrderByTime !== null) {
	const chartInit = new ApexCharts(chartOrderByTime, chartOrderByTimeConfig);
	chartInit.render();
}


const chartDealsOverviewConfig = {
	chart: {
		type: 'area',
		height: 225,
		toolbar: { show: false },
		zoom: { enabled: false },
		sparkline: { enabled: false },
		parentHeightOffset: 0
	},
	series: [{
		name: 'Growth',
		data: [95, 95, 70, 70, 95, 95, 55, 55, 85, 85]
	}],
	stroke: {
		curve: 'smooth',
		width: 2,
		colors: ['var(--bs-info)']
	},
	fill: {
		type: 'solid',
		colors: ['rgba(var(--bs-info-rgb), 0.1)'],
		opacity: 1
	},
	dataLabels: {
		enabled: false
	},
	markers: {
		size: 0,
		colors: ['#FFFFFF'],
		strokeColors: 'var(--bs-info)',
		strokeWidth: 3,
		hover: {
			size: 6
		}
	},
	xaxis: {
		categories: ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep"],
		show: false,
		labels: {
			show: false
		},
		axisBorder: {
			show: false
		},
		axisTicks: {
			show: false
		},
		crosshairs: {
			show: false
		}
	},
	grid: {
		show: false,
		padding: {
			top: 0,
			right: 0,
			bottom: -10,
			left: 0
		}
	},
	yaxis: {
		min: 0,
		max: 100,
		tickAmount: 4,
		show: false,
		labels: {
			show: false,
			formatter: function (val) {
				return val + "%";
			}
		}
	},
	tooltip: {
		enabled: true,
		theme: 'dark',
		x: {
			show: true
		},
		y: {
			formatter: function (val) {
				return val + "%";
			}
		},
		marker: {
			show: true,
			width: 2,
			height: 2,
			fillColors: ['#fff']
		}
	}
}
const chartDealsOverview = document.querySelector("#chartDealsOverview");
if (typeof chartDealsOverview !== undefined && chartDealsOverview !== null) {
	const chartInit = new ApexCharts(chartDealsOverview, chartDealsOverviewConfig);
	chartInit.render();
}


const chartLeadAnalyticsConfig = {
	chart: {
		type: 'area',
		height: 120,
		toolbar: { show: false },
		zoom: { enabled: false },
		sparkline: { enabled: false },
		parentHeightOffset: 0
	},
	series: [{
		name: 'Growth',
		data: [80, 95, 75, 90, 75, 90]
	}],
	stroke: {
		curve: 'smooth',
		width: 2,
		colors: ['var(--bs-primary)']
	},
	fill: {
		type: 'solid',
		colors: ['rgba(var(--bs-primary-rgb), 0.1)'],
		opacity: 1
	},
	dataLabels: {
		enabled: false
	},
	markers: {
		size: 0,
		colors: ['#FFFFFF'],
		strokeColors: 'var(--bs-primary)',
		strokeWidth: 3,
		hover: {
			size: 6
		}
	},
	xaxis: {
		categories: ["Jan", "Feb", "Mar", "Apr", "May", "Jun"],
		show: false,
		labels: {
			show: false
		},
		axisBorder: {
			show: false
		},
		axisTicks: {
			show: false
		},
		crosshairs: {
			show: false
		}
	},
	grid: {
		show: false,
		padding: {
			top: 0,
			right: 0,
			bottom: -10,
			left: 0
		}
	},
	yaxis: {
		min: 0,
		max: 100,
		tickAmount: 4,
		show: false,
		labels: {
			show: false,
			formatter: function (val) {
				return val + "%";
			}
		}
	},
	tooltip: {
		enabled: true,
		theme: 'dark',
		x: {
			show: true
		},
		y: {
			formatter: function (val) {
				return val + "%";
			}
		},
		marker: {
			show: true,
			width: 2,
			height: 2,
			fillColors: ['#fff']
		}
	}
}
const chartLeadAnalytics = document.querySelector("#chartLeadAnalytics");
if (typeof chartLeadAnalytics !== undefined && chartLeadAnalytics !== null) {
	const chartInit = new ApexCharts(chartLeadAnalytics, chartLeadAnalyticsConfig);
	chartInit.render();
}


const statusChartConfig = {
	series: [35],
	chart: {
		type: 'radialBar',
		offsetY: 0,
		height: 350,
		sparkline: { enabled: true }
	},
	plotOptions: {
		radialBar: {
			startAngle: -95,
			endAngle: 95,
			track: {
				background: "rgba(var(--bs-white-rgb), 0.3)",
				strokeWidth: '100%',
				margin: 25
			},
			dataLabels: {
				name: { show: false },
				value: {
					show: true,
					offsetY: -35,
					fontSize: '28px',
					fontFamily: 'var(--bs-body-font-family)',
					fontWeight: 600,
					color: 'var(--bs-white)',
					formatter: function (val) {
						const totalEarning = 5.7;
						return `$${totalEarning}m`;
					}
				},
			}
		}
	},
	grid: {
		padding: {
			top: 0,
			bottom: 0,
			left: 0,
			right: 0
		}
	},
	fill: {
		colors: ['var(--bs-white)']
	}
}
const statusChart = document.querySelector("#statusChart");
if (typeof statusChart !== undefined && statusChart !== null) {
	const chartInit = new ApexCharts(statusChart, statusChartConfig);
	chartInit.render();
}


const chartRevenueConfig = {
	series: [
		{
			name: 'Revenue',
			data: [120, 350, 450, 120, 200, 180, 300, 120, 250, 350, 250, 180]
		}
	],
	chart: {
		type: 'bar',
		height: 280,
		toolbar: {
			show: false
		},
	},
	plotOptions: {
		bar: {
			horizontal: false,
			columnWidth: '70%',
			barHeight: '100%',
			borderRadius: 4
		}
	},
	colors: ['var(--bs-primary)'],
	dataLabels: {
		enabled: false
	},
	stroke: {
		show: true,
	},
	xaxis: {
		categories: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
		axisBorder: {
			color: 'var(--bs-border-color)'
		},
		axisTicks: {
			show: false
		},
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
		max: 500,
		tickAmount: 5,
		labels: {
			formatter: (val) => val + 'K',
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
	fill: {
		type: "gradient",
		gradient: {
			shade: 'light',
			type: "vertical",
			shadeIntensity: 0.1,
			gradientToColors: ["var(--bs-info)"],
			inverseColors: false,
			opacityFrom: 1,
			opacityTo: 0.6,
			stops: [20, 100]
		}
	},
	tooltip: {
		y: {
			formatter: function (val) {
				return "$ " + val + " thousands";
			}
		}
	},
	legend: {
		show: false
	}
};
const chartRevenue = document.querySelector("#chartRevenue");
if (chartRevenue) {
	const chartTabsInit = new ApexCharts(chartRevenue, chartRevenueConfig);
	chartTabsInit.render();

	document.querySelector("#todayRevenueTab").addEventListener("click", () => {
		chartTabsInit.updateOptions({
			xaxis: {
				categories: ['2 AM', '4 AM', '6 AM', '8 AM', '10 AM', '12 PM', '2 PM', '4 PM', '6 PM', '8 PM', '10 PM', '12 AM']
			},
			series: [{
				data: [120, 200, 180, 300, 250, 400, 120, 200, 180, 300, 200, 180]
			}]
		});
	});

	document.querySelector("#weekRevenueTab").addEventListener("click", () => {
		chartTabsInit.updateOptions({
			xaxis: {
				categories: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']
			},
			series: [{
				data: [350, 420, 380, 460, 400, 440, 410]
			}]
		});
	});

	document.querySelector("#monthRevenueTab").addEventListener("click", () => {
		chartTabsInit.updateOptions({
			xaxis: {
				categories: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
			},
			series: [{
				data: [120, 350, 450, 120, 200, 180, 300, 120, 250, 350, 250, 180]
			}]
		});
	});
}


const chartContactsConfig = {
	series: [
		{
			name: 'Total Contacts',
			data: [120, 350, 450, 300, 120, 250]
		}
	],
	chart: {
		type: 'bar',
		height: 120,
		width: 150,
		toolbar: {
			show: false
		},
		zoom: {
			enabled: false
		},
	},
	plotOptions: {
		bar: {
			horizontal: false,
			columnWidth: '60%',
			barHeight: '100%',
			borderRadius: 2
		}
	},
	colors: ['var(--bs-primary)'],
	dataLabels: {
		enabled: false
	},
	stroke: {
		show: true,
	},
	xaxis: {
		show: true,
		axisBorder: {
			show: false
		},
		axisTicks: {
			show: false
		},
		labels: {
			show: false
		}
	},
	yaxis: {
		show: true,
		labels: {
			show: false
		}
	},
	grid: {
		borderColor: 0,
		xaxis: {
			lines: {
				show: false
			}
		},
		yaxis: {
			lines: {
				show: true
			}
		},
		padding: {
			top: 0,
			bottom: 0,
			left: 0,
			right: 0
		}
	},
	fill: {
		type: "gradient",
		gradient: {
			shade: 'light',
			type: "vertical",
			shadeIntensity: 0.1,
			gradientToColors: ["var(--bs-info)"],
			inverseColors: false,
			opacityFrom: 1,
			opacityTo: 0.6,
			stops: [20, 100]
		}
	},
	tooltip: {
		y: {
			formatter: function (val) {
				return "$ " + val + " thousands";
			}
		}
	},
	legend: {
		show: false
	}
};
const chartContacts = document.querySelector("#chartContacts");
if (chartContacts) {
	const chartInit = new ApexCharts(chartContacts, chartContactsConfig);
	chartInit.render();
}


function chartTasksOverviewConfig() {
	const centerTextPlugin = {
		id: 'centerTextPlugin',
		afterDraw(chart) {
			const { ctx, chartArea: { left, right, top, bottom } } = chart;
			const centerX = (left + right) / 2;
			const centerY = (top + bottom) / 2;

			const dataset = chart.data.datasets[0];
			const total = dataset.data.reduce((acc, val) => acc + val, 0);

			let displayValue = total;
			let displayLabel = 'Total Data';

			// Check if hovering
			const activeElements = chart.getActiveElements();
			if (activeElements.length > 0) {
				const firstPoint = activeElements[0];
				displayValue = dataset.data[firstPoint.index];
			}

			ctx.save();
			ctx.textAlign = 'center';
			ctx.textBaseline = 'middle';

			// Dynamic value
			ctx.font = 'bold 20px sans-serif';
			ctx.fillStyle = '#000';
			ctx.fillText(displayValue, centerX, centerY);

			// Dynamic label
			ctx.font = '14px sans-serif';
			ctx.fillStyle = '#000';

			ctx.restore();
		}
	};

	const canvas = document.getElementById('chartTasksOverview');
	if (!canvas) return;
	const ctx = canvas.getContext('2d');

	new Chart(ctx, {
		type: 'doughnut',
		data: {
			labels: ['Salary', 'Bonus', 'Commission', 'Overtime', 'Reimbursement', 'Benefits'],
			datasets: [{
				data: [5, 6, 4],
				backgroundColor: ['#5955D1', '#ACAAE8', '#DEDDF6'],
				borderRadius: 3,
				spacing: 0,
				hoverOffset: 5,
				borderWidth: 3,
				borderColor: '#fff',
				hoverBorderColor: '#fff'
			}]
		},
		options: {
			cutout: '70%',
			devicePixelRatio: 2,
			layout: {
				padding: 0
			},
			plugins: {
				legend: {
					display: false
				},
				tooltip: {
					enabled: false,
					callbacks: {
						label: context => `${context.label}: ${context.formattedValue}`
					}
				}
			}
		},
		plugins: [centerTextPlugin]

	});
}
document.addEventListener('DOMContentLoaded', chartTasksOverviewConfig);


const chartRetentionRateConfig = {
	series: [
		{
			name: 'SMEs',
			data: [40, 80, 70, 20, 20, 25]
		},
		{
			name: 'Startups',
			data: [20, 25, 25, 50, 20, 20]
		},
		{
			name: 'Enterprises',
			data: [20, 20, 20, 20, 15, 15]
		}
	],
	chart: {
		type: 'bar',
		height: 295,
		stacked: true,
		toolbar: {
			show: false
		},
		zoom: {
			enabled: false
		}
	},
	plotOptions: {
		bar: {
			horizontal: false,
			colors: {
				backgroundBarColors: ["rgba(var(--bs-primary-rgb), 0.03)"],
				backgroundBarOpacity: 1
			}
		}
	},
	yaxis: {
		show: false
	},
	states: {
		normal: {
			filter: {
				type: 'none'
			}
		},
		hover: {
			filter: {
				type: 'none'
			}
		},
		active: {
			filter: {
				type: 'none'
			}
		}
	},
	xaxis: {
		categories: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
		axisTicks: {
			show: false
		},
		axisBorder: {
			show: false
		},
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
		offsetY: 0,
		labels: {
			colors: 'var(--bs-body-color)',
			fontSize: '12px',
			fontWeight: '500',
			fontFamily: 'var(--bs-body-font-family)',
		},
		markers: {
            strokeWidth: 0,
        }
	},
	grid: {
		borderColor: 0,
		xaxis: { lines: { show: false } },
		yaxis: { lines: { show: true } }
	},
	fill: {
		colors: ['var(--bs-primary)', 'rgba(var(--bs-primary-rgb), 0.4)', 'rgba(var(--bs-primary-rgb), 0.1)'],
		opacity: 1
	},
	dataLabels: {
		enabled: false
	},
};
const chartRetentionRate = document.querySelector("#chartRetentionRate");
if (chartRetentionRate) {
	const chartInit = new ApexCharts(chartRetentionRate, chartRetentionRateConfig);
	chartInit.render();
}


var reviewSourcesChartConfig = {
	series: [
		{
			name: 'Website',
			data: [41]
		},
		{
			name: 'Google',
			data: [32]
		},
		{
			name: 'App Store',
			data: [16]
		},
		{
			name: 'Play Store',
			data: [9]
		},
		{
			name: 'Social Media',
			data: [2]
		}
	],
	chart: {
		type: 'bar',
		height: 130,
		stacked: true,
		stackType: '100%',
		toolbar: {
			show: false
		},
		animations: {
			enabled: true
		},
	},
	plotOptions: {
		bar: {
			horizontal: true,
			barHeight: '100%',
			borderRadius: 0
		}
	},
	dataLabels: {
		enabled: false
	},
	stroke: {
		width: 0,
		colors: ['#ffffff'],
	},
	xaxis: {
		labels: {
			show: false
		},
		axisBorder: {
			show: false
		},
		axisTicks: {
			show: false
		}
	},
	yaxis: {
		labels: {
			show: false
		}
	},
	grid: {
		show: false,
		padding: {
			top: -15,
			bottom: -15,
			left: -15,
			right: 0
		}
	},
	legend: {
		show: false
	},
	fill: {
		opacity: 1,
		colors: [
			'rgba(var(--bs-primary-rgb), 0.1)',
			'rgba(var(--bs-primary-rgb), 0.25)',
			'rgba(var(--bs-primary-rgb), 0.50)',
			'rgba(var(--bs-primary-rgb), 0.75)',
			'rgba(var(--bs-primary-rgb), 1)'
		]
	},
	tooltip: {
		enabled: true,
		y: {
			formatter: function (val, opts) {
				let original = opts.series[opts.seriesIndex][0];
				return original + "%";
			}
		},
		x: {
			show: false
		},
		marker: {
			show: false
		}
	}

};
const reviewSourcesChart = document.querySelector("#reviewSourcesChart");
if (reviewSourcesChart) {
	var chartInit = new ApexCharts(reviewSourcesChart, reviewSourcesChartConfig);
	chartInit.render();
}


var opportunityTrendChartConfig = {
	series: [
		{
			name: 'Opportunity Value',
			data: [890000, 760000, 1020000, 960000, 880000, 910000, 940000, 1000000, 980000, 920000, 970000, 1010000]
		}
	],
	chart: {
		height: 280,
		type: 'area',
		zoom: { enabled: false },
		toolbar: { show: false },
	},
	colors: [
		"var(--bs-primary)",
		"var(--bs-dark)"
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
		width: 2,
		curve: 'smooth',
		dashArray: 5
	},
	markers: {
		size: 0,
		colors: ['#FFFFFF'],
		strokeColors: 'var(--bs-primary)',
		strokeWidth: 2,
		hover: {
			size: 6
		}
	},
	yaxis: {
		min: 700000,
		max: 1100000,
		tickAmount: 5,
		labels: {
			formatter: function (value) {
				return "$" + (value / 100) + "M";
			},
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
	tooltip: {
		y: {
			formatter: function (val) {
				return "$ " + val + "M";
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
		show: false
	}
};
const opportunityTrendChart = document.querySelector("#opportunityTrendChart");
if (opportunityTrendChart) {
	new ApexCharts(opportunityTrendChart, opportunityTrendChartConfig).render();
}
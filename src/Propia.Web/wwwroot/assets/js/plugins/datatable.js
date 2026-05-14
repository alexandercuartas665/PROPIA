$(document).ready(() => {
	
	// -----------------------------
    // Common language options for all DataTables
    // -----------------------------
    const dtLanguage = {
        search: "",
        searchPlaceholder: 'Search',
        paginate: {
            previous: "<i class='fi fi-rr-angle-left'></i>",
            next: "<i class='fi fi-rr-angle-right'></i>",
            first: "<i class='fi fi-rr-angle-double-left'></i>",
            last: "<i class='fi fi-rr-angle-double-right'></i>"
        }
    };
	
	// -----------------------------
    // Helper function to safely add classes
    // -----------------------------
    const safeAddClass = (selector, classes) => {
        const el = $(selector).first();
        if (el.length) el.addClass(classes);
    };
	
	// -----------------------------
    // Basic DataTable
    // -----------------------------
    if ($('#dt_basic').length) {
        const dtBasicTable = $('#dt_basic').DataTable({
            language: dtLanguage,
            initComplete: () => {
                safeAddClass('#dt_basic_wrapper > .row.mt-2.justify-content-between', 'mx-2 py-2');
            },
        });
    }
	
	// -----------------------------
    // Complex Headers DataTable
    // -----------------------------
    if ($('#dt_ComplexHeaders').length) {
        const dtComplexHeadersTable = $('#dt_ComplexHeaders').DataTable({
            language: dtLanguage,
            initComplete: () => {
                safeAddClass('#dt_ComplexHeaders_wrapper > .row.mt-2.justify-content-between', 'mx-2 py-2');
            },
        });
    }
	
	// -----------------------------
    // Ajax Data DataTable
    // -----------------------------
    if ($('#dt_AjaxData').length) {
        const dtAjaxDataTable = $('#dt_AjaxData').DataTable({
            ajax: 'https://nexlink.layoutdrop.com/php/demo/assets/ajax/arrays.txt',
            language: dtLanguage,
            initComplete: () => {
                safeAddClass('#dt_AjaxData_wrapper > .row.mt-2.justify-content-between', 'mx-2 py-2');
            },
        });
    }
	
	// -----------------------------
    // Extra Detailed DataTable with row details
    // -----------------------------
    if ($('#dt_ExtraDetailed').length) {

        const format = (d) => {
            return (
				'<dl>' +
				'<dt>Full name:</dt>' +
				'<dd>' +
				d.name +
				'</dd>' +
				'<dt>Extension number:</dt>' +
				'<dd>' +
				d.extn +
				'</dd>' +
				'<dt>Extra info:</dt>' +
				'<dd>And any further details here (images etc)...</dd>' +
				'</dl>'
			);
        };

        const dtExtraDetailedTable = $('#dt_ExtraDetailed').DataTable({
            ajax: 'https://nexlink.layoutdrop.com/php/demo/assets/ajax/objects.txt',
            columns: [
                { className: 'dt-control', orderable: false, data: null, defaultContent: '' },
                { data: 'name' },
                { data: 'position' },
                { data: 'office' },
                { data: 'salary' }
            ],
            order: [[1, 'asc']],
            language: dtLanguage,
            initComplete: () => {
                safeAddClass('#dt_ExtraDetailed_wrapper > .row.mt-2.justify-content-between', 'mx-2 py-2');
            },
        });

        // Toggle row details
        $('#dt_ExtraDetailed').on('click', 'td.dt-control', function () {
            let tr = e.target.closest('tr');
			let row = table.row(tr);
		 
			if (row.child.isShown()) {
				// This row is already open - close it
				row.child.hide();
			}
			else {
				// Open this row
				row.child(format(row.data())).show();
			}
        });
    }
	
	// -----------------------------
    // Scroll Vertical DataTable
    // -----------------------------
    if ($('#dt_ScrollVertical').length) {
        const dtScrollVerticalTable = $('#dt_ScrollVertical').DataTable({
            paging: false,
            scrollCollapse: true,
            scrollY: '50vh',
            language: dtLanguage,
            initComplete: () => {
                const dtSearch = $('#dt_ScrollVertical_wrapper .dt-search').detach();
                $('#dt_ScrollVertical_Search').append(dtSearch);
                $('#dt_ScrollVertical_Search .dt-search').prepend('<i class="fi fi-rr-search"></i>');
                $('#dt_ScrollVertical_Search .dt-search label').remove();
                $('#dt_ScrollVertical_wrapper > .row.mt-2.justify-content-between').first().remove();
            },
        });
    }
	
});
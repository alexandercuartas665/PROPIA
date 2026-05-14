document.addEventListener('DOMContentLoaded', () => {

    // Basic Swiper
    if (document.querySelector('.swiperInit')) {
        new Swiper('.swiperInit', {});
    }

    // Navigation Swiper
    if (document.querySelector('.swiperNav')) {
        new Swiper('.swiperNav', {
            navigation: {
                nextEl: '.swiper-button-next',
                prevEl: '.swiper-button-prev'
            }
        });
    }

    // Pagination Swiper
    if (document.querySelector('.swiperPagination')) {
        new Swiper('.swiperPagination', {
            pagination: {
                el: '.swiper-pagination'
            }
        });
    }

    // Dynamic Bullets
    if (document.querySelector('.swiperDynamicBullets')) {
        new Swiper('.swiperDynamicBullets', {
            pagination: {
                el: '.swiper-pagination',
                dynamicBullets: true
            }
        });
    }

    // Progressbar
    if (document.querySelector('.swiperProgressbar')) {
        new Swiper('.swiperProgressbar', {
            pagination: {
                el: '.swiper-pagination',
                type: 'progressbar'
            },
            navigation: {
                nextEl: '.swiper-button-next',
                prevEl: '.swiper-button-prev'
            }
        });
    }

    // Fraction
    if (document.querySelector('.swiperFraction')) {
        new Swiper('.swiperFraction', {
            pagination: {
                el: '.swiper-pagination',
                type: 'fraction'
            },
            navigation: {
                nextEl: '.swiper-button-next',
                prevEl: '.swiper-button-prev'
            }
        });
    }

});

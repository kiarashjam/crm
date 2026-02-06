(function () {
  'use strict';

  var deck = document.querySelector('.slide-deck');
  if (!deck) return;

  var slides = Array.from(deck.querySelectorAll('.slide'));
  var total = slides.length;
  if (total === 0) return;

  var progressBar = document.querySelector('.slide-progress__bar');
  var prevBtn = document.querySelector('.slide-nav__prev');
  var nextBtn = document.querySelector('.slide-nav__next');
  var counterEl = document.querySelector('.slide-nav__counter');

  var current = 0;

  function showSlide(index) {
    index = Math.max(0, Math.min(index, total - 1));
    current = index;
    slides.forEach(function (slide, i) {
      slide.classList.toggle('active', i === current);
    });
    if (progressBar) {
      progressBar.style.width = ((current + 1) / total * 100) + '%';
    }
    if (prevBtn) prevBtn.disabled = current === 0;
    if (nextBtn) nextBtn.disabled = current === total - 1;
    if (counterEl) {
      counterEl.innerHTML = '<strong>' + (current + 1) + '</strong> / ' + total;
    }
    window.location.hash = '#' + (current + 1);
  }

  function goNext() {
    if (current < total - 1) showSlide(current + 1);
  }

  function goPrev() {
    if (current > 0) showSlide(current - 1);
  }

  if (prevBtn) prevBtn.addEventListener('click', goPrev);
  if (nextBtn) nextBtn.addEventListener('click', goNext);

  document.addEventListener('keydown', function (e) {
    if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
    switch (e.key) {
      case 'ArrowRight':
      case ' ':
        e.preventDefault();
        goNext();
        break;
      case 'ArrowLeft':
        e.preventDefault();
        goPrev();
        break;
      case 'Home':
        e.preventDefault();
        showSlide(0);
        break;
      case 'End':
        e.preventDefault();
        showSlide(total - 1);
        break;
    }
  });

  window.addEventListener('hashchange', function () {
    var hash = window.location.hash;
    var n = parseInt(hash.slice(1), 10);
    if (!isNaN(n) && n >= 1 && n <= total) showSlide(n - 1);
  });

  var hash = window.location.hash;
  var initial = parseInt((hash && hash.slice(1)) || '1', 10);
  showSlide(isNaN(initial) || initial < 1 ? 0 : initial - 1);
})();

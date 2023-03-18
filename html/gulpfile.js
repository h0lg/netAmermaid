/// <binding BeforeBuild='less' />
// Less configuration, see https://code.visualstudio.com/Docs/languages/CSS#_transpiling-sass-and-less-into-css
var gulp = require('gulp');
var less = require('gulp-less');

gulp.task('less', function (cb) {
    gulp
        .src('styles.less')
        .pipe(less())
        .pipe(
            gulp.dest(function (f) {
                return f.base;
            })
        );
    cb();
});

gulp.task(
    'default',
    gulp.series('less', function (cb) {
        gulp.watch('*.less', gulp.series('less'));
        cb();
    })
);
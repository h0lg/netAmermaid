/// <binding BeforeBuild='less' />
// Above line allows Visual Studio to trigger the 'less' task before building the project.

// Automate transpilation of .less files, see https://code.visualstudio.com/Docs/languages/CSS#_automating-sassless-compilation
var gulp = require('gulp');
var less = require('gulp-less');

gulp.task('less', function (done) {
    gulp
        .src('styles.less') // source file(s) to process
        .pipe(less()) // pass them through the LESS compiler
        .pipe(gulp.dest(f => f.base)); // Use the base directory of the source file for output

    done(); // signal task completion
});

// the default task that runs when Gulp is executed without any specific task name
gulp.task(
    'default',
    // Run the 'less' task first, then start watching for changes
    gulp.series('less', function (done) {
        gulp.watch('*.less', gulp.series('less'));  // Watch for any changes in .less files and rerun the 'less' task
        done(); // signal task completion
    })
);
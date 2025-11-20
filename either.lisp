(func compose (f g)
  (lambda (x) (f (g x))))

(func curry (f)
  (lambda (x) (lambda (y) (f x y))))

(func id (x) x)

(func const (x)
  (lambda (_) x))

(func singleton (x) (cons x '()))

(func pair (x y) (cons x (singleton y)))
(setq fst head)
(setq snd (compose head tail))

(func left  (x) (pair 'left  x))
(func right (x) (pair 'right x))

(func either! (on-left on-right)
  (lambda (e)
    ((cond (equal 'left  (fst e)) on-left
     (cond (equal 'right (fst e)) on-right))
    (snd e))))

(func bimap (f g)
  (either! (compose left f) (compose right g)))

(func mapLeft (f)
  (bimap f id))

(func mapRight (g)
  (bimap id g))

((mapRight ((curry plus) 5)) (left 'not-right))
((mapRight ((curry plus) 5)) (right 50))

(setq flatRight
  (either! left id))

(flatRight (right (right 52)))
(flatRight (right (left 'err)))
(flatRight (left 'err))

(func flatMapRight (f)
  (compose flatRight (mapRight f)))

(func bind (e f)
  ((flatMapRight f) e))

(bind (right '()) (lambda (a)
  (cond (equal '() a)
    (right 5)
    (left '(expected an empty list)))))

(bind (right '(a)) (lambda (a)
  (cond (equal '() a)
    (right 5)
    (left '(expected an empty list)))))

(bind (left '(some other value)) (lambda (a)
  (cond (equal '() a)
    (right 5)
    (left '(expected an empty list)))))

'f-has-proper-eithers!!!

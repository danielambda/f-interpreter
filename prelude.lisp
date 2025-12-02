(func compose (f g)
  (lambda (x) (f (g x))))

(func curry (f)
  (lambda (x) (lambda (y) (f x y))))

(func uncurry (f)
  (lambda (x y) ((f x) y)))

(func id (x) x)

(func const (x)
  (lambda (_) x))

(func singleton (x) (cons x '()))

(func pair (x y) (cons x (singleton y)))
(setq fst head)
(setq snd (compose head tail))

(func even? (n) (equal 0 (modulo n 2)))
(setq odd? (compose not even?))

(func sqr (n) (times n n))
